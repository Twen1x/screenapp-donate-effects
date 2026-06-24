using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScreenApp.Donations;

/// <summary>Данные пользователя DA (из /api/v1/user/oauth).</summary>
public sealed class DaUser
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("socket_connection_token")]
    public string? SocketConnectionToken { get; set; }
}

/// <summary>Результат рефреша OAuth-токена DA.</summary>
public sealed class DaTokenResult
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public long ExpiresAt { get; set; }
}

/// <summary>Ошибка обращения к API DonationAlerts.</summary>
public sealed class DaApiException : Exception
{
    public DaApiException(string message) : base(message) { }
}

/// <summary>
/// Клиент REST API DonationAlerts: получение данных пользователя, токена подписки на
/// канал Centrifugo и рефреш OAuth-токена. Порт соответствующих функций python-воркера.
/// </summary>
public sealed class DaApi
{
    private const string ApiBase = "https://www.donationalerts.com";

    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public DaApi(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    /// <summary>Получить user_id, имя и socket_connection_token по access token.</summary>
    public async Task<DaUser> GetUserAsync(string accessToken, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/api/v1/user/oauth");
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
        req.Headers.TryAddWithoutValidation("Accept", "application/json");

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            throw new DaApiException($"GetUser: DA вернул HTTP {(int)resp.StatusCode}. Проверьте access token.");
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        if (!doc.RootElement.TryGetProperty("data", out var data))
        {
            throw new DaApiException("GetUser: в ответе DA нет поля data.");
        }

        var user = data.Deserialize<DaUser>(JsonOptions);
        if (user is null || user.Id == 0 || string.IsNullOrEmpty(user.SocketConnectionToken))
        {
            throw new DaApiException("GetUser: неполные данные пользователя DA.");
        }

        return user;
    }

    /// <summary>Получить JWT для подписки на канал Centrifugo.</summary>
    public async Task<string> GetChannelTokenAsync(
        string accessToken, string clientId, string channel, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/api/v1/centrifuge/subscribe")
        {
            Content = JsonContent.Create(new { client = clientId, channels = new[] { channel } }),
        };
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
        req.Headers.TryAddWithoutValidation("Accept", "application/json");

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            throw new DaApiException($"GetChannelToken: DA вернул HTTP {(int)resp.StatusCode}.");
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        if (!doc.RootElement.TryGetProperty("channels", out var channels) ||
            channels.ValueKind != JsonValueKind.Array ||
            channels.GetArrayLength() == 0)
        {
            throw new DaApiException("GetChannelToken: пустой channels в ответе DA.");
        }

        string? token = channels[0].TryGetProperty("token", out var t) ? t.GetString() : null;
        if (string.IsNullOrEmpty(token))
        {
            throw new DaApiException("GetChannelToken: нет token в ответе DA.");
        }

        return token;
    }

    /// <summary>Рефреш access token через OAuth refresh_token grant.</summary>
    public async Task<DaTokenResult> RefreshTokenAsync(
        string refreshToken, string clientId, string clientSecret, CancellationToken ct = default)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = "oauth-user-show oauth-donation-subscribe oauth-donation-index",
        });

        using var resp = await _http.PostAsync($"{ApiBase}/oauth/token", form, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            throw new DaApiException($"RefreshToken: DA вернул HTTP {(int)resp.StatusCode}.");
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        var root = doc.RootElement;
        string? access = root.TryGetProperty("access_token", out var a) ? a.GetString() : null;
        if (string.IsNullOrEmpty(access))
        {
            throw new DaApiException("RefreshToken: нет access_token в ответе DA.");
        }

        string refresh = root.TryGetProperty("refresh_token", out var r) ? r.GetString() ?? refreshToken : refreshToken;
        int expiresIn = root.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var ei) ? ei : 3600;

        return new DaTokenResult
        {
            AccessToken = access,
            RefreshToken = refresh,
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn,
        };
    }

    /// <summary>
    /// Обменять authorization code на токены (OAuth authorization_code grant).
    /// Используется кнопкой «Авторизоваться через браузер» в настройках.
    /// </summary>
    public async Task<DaTokenResult> ExchangeCodeAsync(
        string code, string clientId, string clientSecret, string redirectUri, CancellationToken ct = default)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
        });

        using var resp = await _http.PostAsync($"{ApiBase}/oauth/token", form, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            throw new DaApiException($"ExchangeCode: DA вернул HTTP {(int)resp.StatusCode}.");
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        var root = doc.RootElement;
        string? access = root.TryGetProperty("access_token", out var a) ? a.GetString() : null;
        if (string.IsNullOrEmpty(access))
        {
            throw new DaApiException("ExchangeCode: нет access_token в ответе DA.");
        }

        string refresh = root.TryGetProperty("refresh_token", out var r) ? r.GetString() ?? "" : "";
        int expiresIn = root.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var ei) ? ei : 3600;

        return new DaTokenResult
        {
            AccessToken = access,
            RefreshToken = refresh,
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn,
        };
    }

    /// <summary>Базовый URL авторизации OAuth (для открытия в браузере).</summary>
    public static string BuildAuthorizeUrl(string clientId, string redirectUri)
    {
        const string scope = "oauth-user-show oauth-donation-subscribe oauth-donation-index";
        string encScope = Uri.EscapeDataString(scope);
        string encRedirect = Uri.EscapeDataString(redirectUri);
        return $"{ApiBase}/oauth/authorize?client_id={Uri.EscapeDataString(clientId)}" +
               $"&redirect_uri={encRedirect}&response_type=code&scope={encScope}";
    }
}
