using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SharingBridge.UserService;

/// <summary>
/// HS256 JWT compatible with the Node tokenService (integration + photo verify the same tokens).
/// Uses raw HMAC over the secret string — same as Node crypto.createHmac — including short secrets.
/// </summary>
public sealed class TokenService
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _ttlSeconds;

    public TokenService(IConfiguration config)
    {
        _secret = Env(config, "AUTH_TOKEN_SECRET", "sharingbridge-dev-secret-change-me");
        _issuer = Env(config, "AUTH_TOKEN_ISSUER", "sharingbridge-user-service");
        _audience = Env(config, "AUTH_TOKEN_AUDIENCE", "sharingbridge-clients");
        _ttlSeconds = int.TryParse(config["AUTH_TOKEN_TTL_SECONDS"], out var ttl) ? ttl : 3600;
    }

    public string Mint(string userId, string role, IEnumerable<string> roles)
    {
        var roleList = roles.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
        if (roleList.Count == 0)
        {
            roleList.Add(role);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = """{"alg":"HS256","typ":"JWT"}""";
        var payloadObj = new Dictionary<string, object?>
        {
            ["sub"] = userId,
            ["role"] = role,
            ["roles"] = roleList.ToArray(),
            ["iss"] = _issuer,
            ["aud"] = _audience,
            ["iat"] = now,
            ["exp"] = now + _ttlSeconds
        };
        var payload = JsonSerializer.Serialize(payloadObj);
        var encodedHeader = Base64UrlEncode(Encoding.UTF8.GetBytes(header));
        var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        var signature = Sign($"{encodedHeader}.{encodedPayload}", _secret);
        return $"{encodedHeader}.{encodedPayload}.{signature}";
    }

    public string? TryGetSubFromAuthorization(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return null;
        }

        var trimmed = authorizationHeader.Trim();
        if (!trimmed.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = trimmed["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            var payload = Verify(token);
            if (payload.TryGetProperty("sub", out var sub) && sub.ValueKind == JsonValueKind.String)
            {
                var value = sub.GetString();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public JsonElement Verify(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Token is required.");
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            throw new InvalidOperationException("Token format is invalid.");
        }

        var expected = Sign($"{parts[0]}.{parts[1]}", _secret);
        if (!FixedTimeEquals(expected, parts[2]))
        {
            throw new InvalidOperationException("Token signature is invalid.");
        }

        var json = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        using var doc = JsonDocument.Parse(json);
        var payload = doc.RootElement.Clone();

        if (payload.TryGetProperty("iss", out var iss) && iss.GetString() != _issuer)
        {
            throw new InvalidOperationException("Token issuer is invalid.");
        }

        if (payload.TryGetProperty("aud", out var aud) && aud.GetString() != _audience)
        {
            throw new InvalidOperationException("Token audience is invalid.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!payload.TryGetProperty("exp", out var exp) || exp.ValueKind != JsonValueKind.Number ||
            exp.GetInt64() <= now)
        {
            throw new InvalidOperationException("Token is expired.");
        }

        if (!payload.TryGetProperty("sub", out var sub) ||
            sub.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(sub.GetString()))
        {
            throw new InvalidOperationException("Token subject is invalid.");
        }

        return payload;
    }

    private static string Sign(string data, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = HMACSHA256.HashData(key, bytes);
        return Base64UrlEncode(hash);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }

        return Convert.FromBase64String(s);
    }

    private static string Env(IConfiguration config, string key, string fallback)
    {
        var value = config[key];
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
