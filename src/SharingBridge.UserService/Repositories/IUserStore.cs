namespace SharingBridge.UserService;

public interface IUserStore
{
    Task<IReadOnlyList<string>> GetRolesForUserAsync(string userId, CancellationToken ct);
    Task EnsureRoleAsync(string userId, string role, CancellationToken ct);
    Task<UserDto> GetOrCreateUserAsync(string userId, string? phone, string? email, CancellationToken ct);
    Task<UserDto> FindOrCreateGoogleUserAsync(string googleSub, string? email, string? name, string? picture, CancellationToken ct);
    Task<IReadOnlyList<DonorPreset>> ListDonorPresetsAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<DonorPreset>> ReplaceDonorPresetsAsync(string userId, IEnumerable<DonorPreset> presets, CancellationToken ct);
    Task<IReadOnlyList<DonorPreset>> DeleteDonorPresetAsync(string userId, string restaurantName, string orderUrl, CancellationToken ct);
}
