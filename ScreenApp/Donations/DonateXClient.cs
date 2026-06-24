using System.Diagnostics;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ScreenApp.Diagnostics;
using ScreenApp.Settings;

namespace ScreenApp.Donations;

/// <summary>
/// Слушатель донатов DonateX через SignalR-хаб /api/public-donations-hub.
///
/// SignalR реализован вручную поверх <see cref="ClientWebSocket"/> (JSON-протокол,
/// разделитель записей 0x1E), чтобы не тянуть NuGet-пакет (важно: сборка офлайн).
/// Аутентификация — внешним токеном в query access_token. Событие DonationCreated
/// маппится в общий <see cref="Donation"/> по сумме в рублях (amountInRub).
/// </summary>
public sealed class DonateXClient : IAsyncDisposable
{
    // Разделитель записей SignalR (RecordSeparator).
    private const char Rs = '\u001e';
    private const string WsBase = "wss://donatex.gg/api/public-donations-hub";

    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(15);

    private readonly AppSettings _settings;
    private readonly EventJournal _journal;
    private readonly DonateXApi _api;

    private readonly HashSet<string> _seenIds = new();
    private readonly Queue<string> _seenOrder = new();

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public event Action<Donation>? DonationReceived;
    public event Action<DaConnectionStatus, string>? StatusChanged;

    public DonateXClient(AppSettings settings, EventJournal journal, DonateXApi? api = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _api = api ?? new DonateXApi();
    }

    public bool IsRunning => _loop is not null;

    public void Start()
    {
        if (_loop is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null || _loop is null)
        {
            return;
        }

        _cts.Cancel();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* ожидаемо */ }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _loop = null;
            Status(DaConnectionStatus.Disconnected, "Остановлен");
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Status(DaConnectionStatus.Connecting, "Подключение...");
                await SessionAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Status(DaConnectionStatus.Error, ex.Message);
                Debug.WriteLine($"[DonateX] session error: {ex}");
            }

            if (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(ReconnectDelay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task SessionAsync(CancellationToken ct)
    {
        string token = _settings.DonateXToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new DonateXApiException("Не задан токен DonateX. Откройте настройки и вставьте External token.");
        }

        // SignalR negotiate (необязательно). Если недоступен — подключаемся напрямую.
        string? connectionToken = await _api.NegotiateAsync(token, ct).ConfigureAwait(false);

        string url = $"{WsBase}?access_token={Uri.EscapeDataString(token)}";
        if (!string.IsNullOrEmpty(connectionToken))
        {
            url += $"&id={Uri.EscapeDataString(connectionToken)}";
        }

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(url), ct).ConfigureAwait(false);

        // SignalR handshake: выбираем JSON-протокол.
        await SendRawAsync(ws, "{\"protocol\":\"json\",\"version\":1}" + Rs, ct).ConfigureAwait(false);

        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var pingTask = PingLoopAsync(ws, sessionCts.Token);

        bool handshakeDone = false;
        try
        {
            var buffer = new byte[16 * 1024];
            var sb = new StringBuilder();

            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                sb.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                // Сообщения SignalR разделены символом 0x1E.
                foreach (var chunk in sb.ToString().Split(Rs, StringSplitOptions.RemoveEmptyEntries))
                {
                    string text = chunk.Trim();
                    if (text.Length == 0)
                    {
                        continue;
                    }

                    if (!handshakeDone)
                    {
                        HandleHandshake(text);
                        handshakeDone = true;
                        Status(DaConnectionStatus.Connected, "Подключено, ждём донатов");
                        continue;
                    }

                    HandleMessage(text);
                }
            }
        }
        finally
        {
            sessionCts.Cancel();
            await Task.WhenAny(pingTask, Task.Delay(1000, CancellationToken.None)).ConfigureAwait(false);
        }
    }

    private static void HandleHandshake(string text)
    {
        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.TryGetProperty("error", out var err))
        {
            throw new DonateXApiException($"SignalR handshake отклонён: {err.GetString()}");
        }
    }

    private void HandleMessage(string text)
    {
        JsonElement msg;
        try
        {
            using var doc = JsonDocument.Parse(text);
            msg = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return;
        }

        if (!msg.TryGetProperty("type", out var typeEl) || !typeEl.TryGetInt32(out int type))
        {
            return;
        }

        // type 1 = invocation; всё остальное (ping=6, close=7, completion) игнорируем.
        if (type != 1)
        {
            return;
        }

        if (!msg.TryGetProperty("target", out var targetEl) ||
            !string.Equals(targetEl.GetString(), "DonationCreated", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!msg.TryGetProperty("arguments", out var args) ||
            args.ValueKind != JsonValueKind.Array ||
            args.GetArrayLength() == 0)
        {
            return;
        }

        var donation = MapDonation(args[0]);
        if (donation is null)
        {
            return;
        }

        if (donation.DonationId is { } did)
        {
            if (!_seenIds.Add(did))
            {
                return; // дубль
            }
            _seenOrder.Enqueue(did);
            while (_seenOrder.Count > 2000)
            {
                _seenIds.Remove(_seenOrder.Dequeue());
            }
        }

        string extra = string.IsNullOrWhiteSpace(donation.Message) ? "" : $": {donation.Message}";
        string testMark = donation.IsTest ? "[ТЕСТ] " : "";
        _journal.Append($"💰 {testMark}DonateX: {donation.Amount:0.##} ₽ от {donation.DisplayName}{extra}");

        DonationReceived?.Invoke(donation);
    }

    /// <summary>Маппинг payload DonationCreated в общий <see cref="Donation"/> (сумма в рублях).</summary>
    public static Donation? MapDonation(JsonElement d)
    {
        if (d.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        decimal amount = ReadDecimal(d, "amountInRub");
        if (amount <= 0)
        {
            amount = ReadDecimal(d, "amount");
        }

        return new Donation
        {
            DonationId = d.TryGetProperty("id", out var id) ? id.GetString() : null,
            Amount = amount,
            Username = ReadString(d, "username"),
            Message = ReadString(d, "message"),
            IsTest = d.TryGetProperty("isTest", out var t) && t.ValueKind == JsonValueKind.True,
        };
    }

    private static decimal ReadDecimal(JsonElement d, string key)
    {
        if (!d.TryGetProperty(key, out var el))
        {
            return 0m;
        }

        return el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetDecimal(out var n) => n,
            JsonValueKind.String when decimal.TryParse(el.GetString(), NumberStyles.Any,
                CultureInfo.InvariantCulture, out var p) => p,
            _ => 0m,
        };
    }

    private static string? ReadString(JsonElement d, string key) =>
        d.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private async Task PingLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                await Task.Delay(PingInterval, ct).ConfigureAwait(false);
                await SendRawAsync(ws, "{\"type\":6}" + Rs, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* норм */ }
        catch (Exception ex) { Debug.WriteLine($"[DonateX] ping stopped: {ex.Message}"); }
    }

    private static async Task SendRawAsync(ClientWebSocket ws, string text, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct)
            .ConfigureAwait(false);
    }

    private void Status(DaConnectionStatus status, string message)
    {
        StatusChanged?.Invoke(status, message);
        _journal.Append($"[DonateX] {message}");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
}
