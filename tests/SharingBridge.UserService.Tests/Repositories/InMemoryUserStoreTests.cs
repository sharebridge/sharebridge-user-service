using SharingBridge.UserService;

namespace SharingBridge.UserService.Tests.Repositories;

public class InMemoryUserStoreTests
{
    [Fact]
    public async Task FindOrCreateGoogleUser_is_stable_for_same_sub()
    {
        var store = new InMemoryUserStore();
        var first = await store.FindOrCreateGoogleUserAsync(
            "google-sub-1", "a@example.com", "Ann", null, CancellationToken.None);
        var second = await store.FindOrCreateGoogleUserAsync(
            "google-sub-1", "a@example.com", "Ann Updated", null, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Ann Updated", second.Name);
    }

    [Fact]
    public async Task Replace_and_list_presets_roundtrip()
    {
        var store = new InMemoryUserStore();
        await store.GetOrCreateUserAsync("u1", null, null, CancellationToken.None);

        var saved = await store.ReplaceDonorPresetsAsync(
            "u1",
            [
                new DonorPreset
                {
                    RestaurantName = "Kitchen",
                    OrderUrl = "https://example.com/k",
                    MenuItems = ["Meal"],
                    AppName = "swiggy",
                    Source = "manual",
                    Confidence = 1
                }
            ],
            CancellationToken.None);

        Assert.Single(saved);
        var listed = await store.ListDonorPresetsAsync("u1", CancellationToken.None);
        Assert.Single(listed);
        Assert.Equal("Kitchen", listed[0].RestaurantName);
    }
}
