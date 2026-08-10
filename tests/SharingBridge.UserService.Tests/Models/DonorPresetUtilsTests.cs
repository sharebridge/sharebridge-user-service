using SharingBridge.UserService;

namespace SharingBridge.UserService.Tests.Models;

public class DonorPresetUtilsTests
{
    [Fact]
    public void ValidatePreset_rejects_missing_restaurant_name()
    {
        var err = DonorPresetUtils.ValidatePreset(
            new DonorPreset
            {
                RestaurantName = " ",
                OrderUrl = "https://example.com",
                MenuItems = ["Meal"],
                AppName = "swiggy",
                Source = "manual",
                Confidence = 1
            },
            0);

        Assert.Equal("presets[0].restaurant_name must be a non-empty string.", err);
    }

    [Fact]
    public void NormalizeForStorage_dedupes_by_restaurant_and_url()
    {
        var presets = DonorPresetUtils.NormalizeForStorage(
            "u1",
            [
                new DonorPreset
                {
                    RestaurantName = "A",
                    OrderUrl = "https://x",
                    MenuItems = ["1"],
                    AppName = "swiggy",
                    Source = "manual",
                    Confidence = 0.5
                },
                new DonorPreset
                {
                    RestaurantName = "A",
                    OrderUrl = "https://x",
                    MenuItems = ["2"],
                    AppName = "swiggy",
                    Source = "manual",
                    Confidence = 0.9
                }
            ]);

        Assert.Single(presets);
        Assert.Equal(["2"], presets[0].MenuItems);
    }
}
