using System.Diagnostics;
using System.Net;
using System.Text;

namespace ScreenApp.Donations;

/// <summary>
/// Локальный поток авторизации DonationAlerts OAuth (authorization_code) без сайта.
///
/// Поднимает локальный HTTP-листенер на <see cref="RedirectUri"/>, открывает браузер на
/// странице авторизации DA, ловит redirect с ?code=..., обменивает код на токены.
///
/// Важно: этот же <see cref="RedirectUri"/> нужно указать в настройках приложения DA
/// (раздел Redirect URI), иначе DA отклонит авторизацию.
/// </summary>
public sealed class OAuthFlow
{
    /// <summary>Фиксированный локальный redirect URI (зарегистрируйте его в приложении DA).</summary>
    public const string RedirectUri = "http://localhost:7777/da-callback/";

    private readonly DaApi _api;

    public OAuthFlow(DaApi? api = null)
    {
        _api = api ?? new DaApi();
    }

    /// <summary>
    /// Выполнить авторизацию: открыть браузер и дождаться кода. Возвращает токены.
    /// </summary>
    public async Task<DaTokenResult> AuthorizeAsync(
        string clientId, string clientSecret, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new DaApiException("Укажите Client ID и Client Secret приложения DonationAlerts.");
        }

        using var listener = new HttpListener();
        listener.Prefixes.Add(RedirectUri);
        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            throw new DaApiException(
                $"Не удалось открыть локальный порт для авторизации: {ex.Message}. " +
                "Запустите приложение от администратора или освободите порт 7777.");
        }

        string authUrl = DaApi.BuildAuthorizeUrl(clientId, RedirectUri);
        Process.Start(new ProcessStartInfo { FileName = authUrl, UseShellExecute = true });

        // Ждём редирект с кодом (с учётом отмены/таймаута).
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(3));

        HttpListenerContext context;
        try
        {
            var getContext = listener.GetContextAsync();
            var completed = await Task.WhenAny(getContext, Task.Delay(Timeout.Infinite, timeoutCts.Token))
                .ConfigureAwait(false);

            if (completed != getContext)
            {
                throw new DaApiException("Время ожидания авторизации истекло.");
            }

            context = await getContext.ConfigureAwait(false);
        }
        finally
        {
            // listener закроется в using.
        }

        string? code = context.Request.QueryString["code"];
        string? error = context.Request.QueryString["error"];

        await WriteResponseAsync(context,
            error is null
                ? "Авторизация прошла успешно. Можно вернуться в приложение и закрыть эту вкладку."
                : $"Ошибка авторизации: {error}").ConfigureAwait(false);

        if (!string.IsNullOrEmpty(error))
        {
            throw new DaApiException($"DA вернул ошибку авторизации: {error}");
        }

        if (string.IsNullOrEmpty(code))
        {
            throw new DaApiException("DA не вернул код авторизации.");
        }

        return await _api.ExchangeCodeAsync(code, clientId, clientSecret, RedirectUri, ct)
            .ConfigureAwait(false);
    }

    private static async Task WriteResponseAsync(HttpListenerContext context, string message)
    {
        string html = $"<!doctype html><html lang=\"ru\"><head><meta charset=\"utf-8\">" +
                      $"<title>ScreenApp</title></head><body style=\"font-family:sans-serif;" +
                      $"background:#1e1830;color:#fff;text-align:center;padding-top:80px\">" +
                      $"<h2>ScreenApp</h2><p>{WebUtility.HtmlEncode(message)}</p></body></html>";
        byte[] buffer = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer).ConfigureAwait(false);
        context.Response.OutputStream.Close();
    }
}
