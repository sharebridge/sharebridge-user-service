using System.Text.Json;
using Google.Apis.Auth;

namespace SharingBridge.UserService;

public sealed class GoogleAuthService
{
    private static readonly Uri UserInfoUrl = new("https://openidconnect.googleapis.com/v1/userinfo");
    private readonly string[] _audiences;
    private readonly IHttpClientFactory _httpClientFactory;

    public GoogleAuthService(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _audiences = ParseClientIds(config);
    }

    public IReadOnlyList<string> Audiences => _audiences;

    public async Task<GoogleProfile> VerifyAccessTokenAsync(string accessToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("access_token is required.");
        }

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Trim());
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await client.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
        var root = doc.RootElement;

        if (!response.IsSuccessStatusCode)
        {
            var detail =
                root.TryGetProperty("error_description", out var ed) ? ed.GetString()
                : root.TryGetProperty("error", out var err) ? err.GetString()
                : $"HTTP {(int)response.StatusCode}";
            throw new InvalidOperationException($"Google access token validation failed: {detail}");
        }

        if (!root.TryGetProperty("sub", out var subEl) || string.IsNullOrWhiteSpace(subEl.GetString()))
        {
            throw new InvalidOperationException("Google token missing subject.");
        }

        return new GoogleProfile
        {
            GoogleSub = subEl.GetString()!,
            Email = root.TryGetProperty("email", out var email) ? email.GetString() : null,
            EmailVerified = root.TryGetProperty("email_verified", out var ev) && ev.ValueKind == JsonValueKind.True,
            Name = root.TryGetProperty("name", out var name) ? name.GetString() : null,
            Picture = root.TryGetProperty("picture", out var picture) ? picture.GetString() : null
        };
    }

    public async Task<GoogleProfile> VerifyIdTokenAsync(string idToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new InvalidOperationException("id_token is required.");
        }

        if (_audiences.Length == 0)
        {
            throw new InvalidOperationException(
                "GOOGLE_CLIENT_ID (or GOOGLE_CLIENT_ID_WEB / _ANDROID) is not configured.");
        }

        var payload = await GoogleJsonWebSignature.ValidateAsync(
            idToken.Trim(),
            new GoogleJsonWebSignature.ValidationSettings { Audience = _audiences });

        if (string.IsNullOrWhiteSpace(payload.Subject))
        {
            throw new InvalidOperationException("Google token missing subject.");
        }

        return new GoogleProfile
        {
            GoogleSub = payload.Subject,
            Email = payload.Email,
            EmailVerified = payload.EmailVerified,
            Name = payload.Name,
            Picture = payload.Picture
        };
    }

    private static string[] ParseClientIds(IConfiguration config)
    {
        var combined = new[]
            {
                config["GOOGLE_CLIENT_ID"],
                config["GOOGLE_CLIENT_ID_WEB"],
                config["GOOGLE_CLIENT_ID_ANDROID"],
                config["GOOGLE_CLIENT_ID_IOS"]
            }
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .SelectMany(v => v!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return combined;
    }
}
