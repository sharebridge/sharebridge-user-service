using System.Text.Json.Serialization;

namespace SharingBridge.UserService;

public sealed class GoogleAuthRequest
{
    [JsonPropertyName("id_token")]
    public string? IdToken { get; set; }

    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("client_type")]
    public string? ClientType { get; set; }
}

public sealed class AuthResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("user")]
    public UserDto User { get; set; } = new();
}

public sealed class UserDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = "";

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = Roles.Donor;

    [JsonPropertyName("google_sub")]
    public string? GoogleSub { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("picture")]
    public string? Picture { get; set; }

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";
}

public sealed class DonorPreset
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("restaurant_name")]
    public string RestaurantName { get; set; } = "";

    [JsonPropertyName("order_url")]
    public string OrderUrl { get; set; } = "";

    [JsonPropertyName("menu_items")]
    public List<string> MenuItems { get; set; } = [];

    [JsonPropertyName("app_name")]
    public string AppName { get; set; } = "";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("saved_at")]
    public string SavedAt { get; set; } = "";
}

public sealed class PresetsBody
{
    [JsonPropertyName("presets")]
    public List<DonorPreset>? Presets { get; set; }
}

public sealed class DeletePresetBody
{
    [JsonPropertyName("restaurant_name")]
    public string? RestaurantName { get; set; }

    [JsonPropertyName("order_url")]
    public string? OrderUrl { get; set; }
}

public sealed class GoogleProfile
{
    public string GoogleSub { get; set; } = "";
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
    public string? Name { get; set; }
    public string? Picture { get; set; }
}

public sealed class ErrorBody
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; set; }
}
