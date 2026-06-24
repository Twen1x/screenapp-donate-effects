using System.Net;
using System.Net.Sockets;

namespace ScreenApp.Diagnostics;

/// <summary>Итог одной проверки в диагностике.</summary>
public sealed record DiagnosticCheck(string Name, bool Ok, string Detail);

/// <summary>
/// Сетевая самодиагностика: проверяет доступность хостов DonationAlerts и локального
/// порта авторизации. Помогает понять причину «Unable to connect to the remote server»
/// (как правило — провайдер блокирует <c>centrifugo.donationalerts.com</c>, лечится VPN).
/// </summary>
public static class NetworkDiagnostics
{
    private const string ApiHost = "www.donationalerts.com";
    private const string SocketHost = "centrifugo.donationalerts.com";
    private const string DonateXHost = "donatex.gg";
    private const int OAuthPort = 7777;

    /// <summary>Прогнать все проверки и вернуть результаты в порядке показа.</summary>
    public static async Task<List<DiagnosticCheck>> RunAsync(CancellationToken ct = default)
    {
        var results = new List<DiagnosticCheck>
        {
            await CheckHostAsync("DonationAlerts — сайт (REST/OAuth)", ApiHost, ct).ConfigureAwait(false),
            await CheckHostAsync("DonationAlerts — сервер донатов (WebSocket)", SocketHost, ct).ConfigureAwait(false),
            await CheckHostAsync("DonateX — сервер донатов", DonateXHost, ct).ConfigureAwait(false),
            CheckLocalPort("Локальный порт авторизации", OAuthPort),
        };
        return results;
    }

    /// <summary>Проверить DNS-резолв и TCP-соединение по 443 к хосту.</summary>
    private static async Task<DiagnosticCheck> CheckHostAsync(string name, string host, CancellationToken ct)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return new DiagnosticCheck(name,
                false,
                $"{host}: имя не разрешается (DNS). Скорее всего, домен блокируется провайдером. Включите VPN.");
        }

        if (addresses.Length == 0)
        {
            return new DiagnosticCheck(name, false, $"{host}: DNS не вернул адрес. Похоже на блокировку. Включите VPN.");
        }

        try
        {
            using var client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(6));

            await client.ConnectAsync(addresses[0], 443, timeoutCts.Token).ConfigureAwait(false);
            return new DiagnosticCheck(name, true, $"{host} ({addresses[0]}): соединение установлено.");
        }
        catch (Exception)
        {
            return new DiagnosticCheck(name,
                false,
                $"{host} ({addresses[0]}): порт 443 недоступен. Возможна блокировка провайдером или файрволом. Включите VPN.");
        }
    }

    /// <summary>Проверить, свободен ли локальный порт для OAuth-колбэка.</summary>
    private static DiagnosticCheck CheckLocalPort(string name, int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return new DiagnosticCheck(name, true, $"Порт {port} свободен — авторизация через браузер сработает.");
        }
        catch (Exception)
        {
            return new DiagnosticCheck(name,
                false,
                $"Порт {port} занят другой программой. Закройте её или запустите приложение от администратора.");
        }
    }
}
