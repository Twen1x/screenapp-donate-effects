using System.Diagnostics;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ScreenApp.Diagnostics;
using ScreenApp.Settings;

namespace ScreenApp.Donations;

/// <summary>Состояние подключения к DonationAlerts.</summary>
public enum DaConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    AuthError,
    Error,
}

/// <summary>
/// Слушатель донатов DonationAlerts через Centrifugo WebSocket. Полный порт логики
/// python-воркера (worker.py) в C#, но без сайта: токены берутся из локальных настроек,
/// донаты доставляются событием <see cref="DonationReceived"/>.
///
/// Жизненный цикл: <see cref="Start"/> запускает фоновый цикл с реконнектом и бэкоффом;
/// <see cref="StopAsync"/> останавливает. Дедупликация донатов — по DonationId.
/// </summary>
public sealed class DonationAlertsClient : IAsyncDisposable
{
    private const string CentrifugoUrl = "wss://centrifugo.donationalerts.com/connection/websocket";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan WatchdogTimeout = TimeSpan.FromSeconds(90);
    private const int TokenRefreshBeforeSeconds = 120;

    private readonly AppSettings _settings;
    private readonly DaApi _api;
    private readonly Action<AppSettings>? _saveSettings;
    private readonly EventJournal _journal;

    private readonly HashSet<string> _seenIds = new();
    private readonly Queue<string> _seenOrder = new();

    private CancellationTokenSource? _cts;
    private Task? _loop;

    /// <summary>Донат получен (вызывается из фонового потока — слушатель сам маршалит в UI).</summary>
    public event Action<Donation>? DonationReceived;

    /// <summary>Изменение состояния подключения (для трея/настроек).</summary>
    public event Action<DaConnectionStatus, string>? StatusChanged;

    public DonationAlertsClient(AppSettings settings, EventJournal journal,
        DaApi? api = null, Action<AppSettings>? saveSettings = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _api = api ?? new DaApi();
        _saveSettings = saveSettings;
    }

    /// <summary>Запущен ли фоновый цикл.</summary>
    public bool IsRunning => _loop is not null;

    /// <summary>Запустить слушатель (идемпотентно).</summary>
    public void Start()
    {
        if (_loop is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    /// <summary>Остановить слушатель.</summary>
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
            catch (DaApiException ex)
            {
                Status(DaConnectionStatus.AuthError, ex.Message);
            }
            catch (Exception ex)
            {
                Status(DaConnectionStatus.Error, ex.Message);
                Debug.WriteLine($"[DAClient] session error: {ex}");
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

    /// <summary>Одна сессия WebSocket. Завершается (исключением/возвратом) → внешний цикл реконнектит.</summary>
    private async Task SessionAsync(CancellationToken ct)
    {
        string accessToken = await GetAccessTokenAsync(ct).ConfigureAwait(false);

        var user = await _api.GetUserAsync(accessToken, ct).ConfigureAwait(false);
        string channel = $"$alerts:donation_{user.Id}";

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Origin", "https://www.donationalerts.com");
        await ws.ConnectAsync(new Uri(CentrifugoUrl), ct).ConfigureAwait(false);

        int msgId = 0;
        int connectId = ++msgId;
        await SendAsync(ws, new { @params = new { token = user.SocketConnectionToken }, id = connectId }, ct)
            .ConfigureAwait(false);

        int? subscribeId = null;
        var lastActivity = DateTime.UtcNow;

        // Пинг и сторож в фоне; завершают приём при простое.
        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var pingTask = PingLoopAsync(ws, sessionCts.Token);
        var watchdogTask = WatchdogAsync(() => lastActivity, sessionCts.Token);

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

                lastActivity = DateTime.UtcNow;

                // Centrifugo может слать несколько JSON-сообщений через \n.
                foreach (var chunk in sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    await HandleChunkAsync(ws, chunk.Trim(), accessToken, channel,
                        connectId, () => ++msgId, id => subscribeId = id, subscribeId, ct)
                        .ConfigureAwait(false);
                }
            }
        }
        finally
        {
            sessionCts.Cancel();
            await Task.WhenAny(Task.WhenAll(pingTask, watchdogTask), Task.Delay(1000, CancellationToken.None))
                .ConfigureAwait(false);
        }
    }

    private async Task HandleChunkAsync(
        ClientWebSocket ws, string chunk, string accessToken, string channel,
        int connectId, Func<int> nextId, Action<int> setSubscribeId, int? subscribeId,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(chunk))
        {
            return;
        }

        JsonElement msg;
        try
        {
            using var doc = JsonDocument.Parse(chunk);
            msg = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return;
        }

        // Пустой объект {} — пинг от сервера, отвечаем тем же.
        if (msg.ValueKind == JsonValueKind.Object && !msg.EnumerateObject().Any())
        {
            await SendRawAsync(ws, "{}", ct).ConfigureAwait(false);
            return;
        }

        int? id = msg.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var i) ? i : null;

        // Ответ на connect → берём client_id, получаем channel token, подписываемся.
        if (id == connectId && msg.TryGetProperty("result", out var connResult))
        {
            string? clientId = connResult.TryGetProperty("client", out var c) ? c.GetString() : null;
            if (string.IsNullOrEmpty(clientId))
            {
                throw new DaApiException("Нет client_id в ответе connect.");
            }

            string chToken = await _api.GetChannelTokenAsync(accessToken, clientId, channel, ct)
                .ConfigureAwait(false);

            int subId = nextId();
            setSubscribeId(subId);
            await SendAsync(ws, new
            {
                @params = new { channel, token = chToken },
                method = 1,
                id = subId,
            }, ct).ConfigureAwait(false);
            return;
        }

        // Ответ на subscribe → подключены.
        if (subscribeId.HasValue && id == subscribeId.Value)
        {
            Status(DaConnectionStatus.Connected, "Подключено, ждём донатов");
            return;
        }

        // Push с донатом.
        var donation = DonationParser.Parse(msg);
        if (donation is not null)
        {
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
            _journal.Append($"💰 {testMark}DonationAlerts: {donation.Amount:0.##} ₽ от {donation.DisplayName}{extra}");

            DonationReceived?.Invoke(donation);
        }
    }

    private async Task PingLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                await Task.Delay(PingInterval, ct).ConfigureAwait(false);
                await SendRawAsync(ws, "{}", ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* норм */ }
        catch (Exception ex) { Debug.WriteLine($"[DAClient] ping stopped: {ex.Message}"); }
    }

    private static async Task WatchdogAsync(Func<DateTime> lastActivity, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
                if (DateTime.UtcNow - lastActivity() > WatchdogTimeout)
                {
                    throw new TimeoutException("Нет активности от Centrifugo — реконнект.");
                }
            }
        }
        catch (OperationCanceledException) { /* норм */ }
    }

    /// <summary>Получить валидный access token: при необходимости рефрешим.</summary>
    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bool valid = !string.IsNullOrEmpty(_settings.DaAccessToken) &&
                     (_settings.DaTokenExpiresAt == 0 || _settings.DaTokenExpiresAt - now > TokenRefreshBeforeSeconds);

        if (valid)
        {
            return _settings.DaAccessToken;
        }

        bool canRefresh = !string.IsNullOrEmpty(_settings.DaRefreshToken) &&
                          !string.IsNullOrEmpty(_settings.DaClientId) &&
                          !string.IsNullOrEmpty(_settings.DaClientSecret);

        if (canRefresh)
        {
            var refreshed = await _api.RefreshTokenAsync(
                _settings.DaRefreshToken, _settings.DaClientId, _settings.DaClientSecret, ct).ConfigureAwait(false);

            _settings.DaAccessToken = refreshed.AccessToken;
            _settings.DaRefreshToken = refreshed.RefreshToken;
            _settings.DaTokenExpiresAt = refreshed.ExpiresAt;
            _saveSettings?.Invoke(_settings);

            return refreshed.AccessToken;
        }

        if (string.IsNullOrEmpty(_settings.DaAccessToken))
        {
            throw new DaApiException("Не задан access token DonationAlerts. Откройте настройки и авторизуйтесь.");
        }

        // Токен есть, но возможно истёк и рефреша нет — пробуем как есть (DA вернёт 401).
        return _settings.DaAccessToken;
    }

    private static Task SendAsync(ClientWebSocket ws, object payload, CancellationToken ct) =>
        SendRawAsync(ws, JsonSerializer.Serialize(payload), ct);

    private static async Task SendRawAsync(ClientWebSocket ws, string json, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct)
            .ConfigureAwait(false);
    }

    private void Status(DaConnectionStatus status, string message)
    {
        StatusChanged?.Invoke(status, message);
        _journal.Append($"[DonationAlerts] {message}");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
}
