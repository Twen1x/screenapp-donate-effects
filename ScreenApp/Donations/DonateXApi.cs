using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ScreenApp.Donations;

/// <summary>Ошибка обращения к API DonateX.</summary>
public sealed class DonateXApiException : Exception
{
    public DonateXApiException(string message) : base(message) { }
}

/// <summary>
/// REST-помощник DonateX: проверка токена (профиль стримера) и SignalR-negotiate
/// для подключения к хабу донатов. Аутентификация — внешним токеном (Bearer),
/// бессрочным, из кабинета DonateX (Настройки → Api).
/// </summary>
public sealed class DonateXApi
{
    public const string ApiBase = "https://donatex.gg/api";
    public const string HubPath = "/public-donations-hub";

    private readonly HttpClient _http;

    public DonateXApi(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    /// <summary>Проверить токен: вернуть ник стримера (GET /v1/user/me).</summary>
    public async Task<string> GetUsernameAsync(string token, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/v1/user/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            throw new DonateXApiException($"DonateX вернул HTTP {(int)resp.StatusCode}. Проверьте токен.");
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        string? username = doc.RootElement.TryGetProperty("username", out var u) ? u.GetString() : null;
        return string.IsNullOrEmpty(username) ? "(без имени)" : username;
    }

    /// <summary>
    /// SignalR negotiate: вернуть connectionToken для подключения к хабу, либо null,
    /// если negotiate недоступен (тогда подключаемся напрямую, skipNegotiation).
    /// </summary>
    public async Task<string?> NegotiateAsync(string token, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post,
                $"{ApiBase}{HubPath}/negotiate?negotiateVersion=1");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = new StringContent("");

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var root = doc.RootElement;
            if (root.TryGetProperty("connectionToken", out var ctok) && ctok.GetString() is { Length: > 0 } token1)
            {
                return token1;
            }
            if (root.TryGetProperty("connectionId", out var cid) && cid.GetString() is { Length: > 0 } token2)
            {
                return token2;
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
