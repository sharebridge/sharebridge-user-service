namespace SharingBridge.UserService;

public static class DonorPresetUtils
{
    public static string KeyFromPair(string? restaurantName, string? orderUrl) =>
        $"{(restaurantName ?? "").Trim()}::{(orderUrl ?? "").Trim()}";

    public static string KeyForPreset(DonorPreset preset) =>
        KeyFromPair(preset.RestaurantName, preset.OrderUrl);

    public static List<DonorPreset> NormalizeForStorage(string userId, IEnumerable<DonorPreset> presets)
    {
        var now = DateTime.UtcNow.ToString("O");
        var deduped = new Dictionary<string, DonorPreset>();
        foreach (var preset in presets)
        {
            var id = string.IsNullOrWhiteSpace(preset.Id)
                ? $"{userId}-preset-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Random.Shared.Next(0x100000):x}"
                : preset.Id.Trim();
            var normalized = new DonorPreset
            {
                Id = id,
                RestaurantName = preset.RestaurantName,
                OrderUrl = preset.OrderUrl,
                MenuItems = preset.MenuItems ?? [],
                AppName = preset.AppName,
                Source = preset.Source,
                Confidence = preset.Confidence,
                SavedAt = string.IsNullOrWhiteSpace(preset.SavedAt) ? now : preset.SavedAt
            };
            deduped[KeyForPreset(normalized)] = normalized;
        }

        return deduped.Values.ToList();
    }

    public static string? ValidatePreset(DonorPreset? preset, int index)
    {
        if (preset is null)
        {
            return $"presets[{index}] must be an object.";
        }

        if (string.IsNullOrWhiteSpace(preset.RestaurantName))
        {
            return $"presets[{index}].restaurant_name must be a non-empty string.";
        }

        if (string.IsNullOrWhiteSpace(preset.OrderUrl))
        {
            return $"presets[{index}].order_url must be a non-empty string.";
        }

        if (preset.MenuItems is null || preset.MenuItems.Any(item => item is null))
        {
            return $"presets[{index}].menu_items must be an array of strings.";
        }

        if (string.IsNullOrWhiteSpace(preset.AppName))
        {
            return $"presets[{index}].app_name must be a non-empty string.";
        }

        if (string.IsNullOrWhiteSpace(preset.Source))
        {
            return $"presets[{index}].source must be a non-empty string.";
        }

        if (double.IsNaN(preset.Confidence))
        {
            return $"presets[{index}].confidence must be a number.";
        }

        return null;
    }
}
