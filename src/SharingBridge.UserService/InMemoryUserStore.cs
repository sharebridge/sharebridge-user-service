using System.Collections.Concurrent;
using System.Text.Json;

namespace SharingBridge.UserService;

/// <summary>In-memory store for unit tests (mirrors Node file UserStore behaviour).</summary>
public sealed class InMemoryUserStore : IUserStore
{
    private readonly ConcurrentDictionary<string, UserDto> _users = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _roles = new();
    private readonly ConcurrentDictionary<string, List<DonorPreset>> _presets = new();

    public Task EnsureRoleAsync(string userId, string role, CancellationToken ct)
    {
        var set = _roles.GetOrAdd(userId, _ => new HashSet<string>(StringComparer.Ordinal));
        lock (set)
        {
            set.Add(role);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetRolesForUserAsync(string userId, CancellationToken ct)
    {
        if (!_roles.TryGetValue(userId, out var set))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        lock (set)
        {
            return Task.FromResult<IReadOnlyList<string>>(set.OrderBy(r => r, StringComparer.Ordinal).ToList());
        }
    }

    public async Task<UserDto> GetOrCreateUserAsync(string userId, string? phone, string? email, CancellationToken ct)
    {
        var user = _users.GetOrAdd(userId, id => new UserDto
        {
            Id = id,
            UserId = id,
            CreatedAt = DateTime.UtcNow.ToString("O"),
            Role = Roles.Donor
        });

        if (!string.IsNullOrWhiteSpace(phone))
        {
            user.Phone = phone.Trim();
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            user.Email = email.Trim();
        }

        await EnsureRoleAsync(userId, Roles.Donor, ct);
        var roles = await GetRolesForUserAsync(userId, ct);
        user.Role = roles.Contains(Roles.Coordinator) ? Roles.Coordinator : Roles.Donor;
        return Clone(user);
    }

    public async Task<UserDto> FindOrCreateGoogleUserAsync(
        string googleSub, string? email, string? name, string? picture, CancellationToken ct)
    {
        var existing = _users.Values.FirstOrDefault(u => u.GoogleSub == googleSub);
        if (existing is not null)
        {
            if (!string.IsNullOrWhiteSpace(email)) existing.Email = email.Trim();
            if (!string.IsNullOrWhiteSpace(name)) existing.Name = name.Trim();
            if (!string.IsNullOrWhiteSpace(picture)) existing.Picture = picture.Trim();
            await EnsureRoleAsync(existing.Id, Roles.Donor, ct);
            var roles = await GetRolesForUserAsync(existing.Id, ct);
            existing.Role = roles.Contains(Roles.Coordinator) ? Roles.Coordinator : Roles.Donor;
            return Clone(existing);
        }

        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(googleSub));
        var userId = $"u_{Convert.ToHexString(hash).ToLowerInvariant()[..16]}";
        var created = new UserDto
        {
            Id = userId,
            UserId = userId,
            GoogleSub = googleSub,
            Email = email,
            Name = name,
            Picture = picture,
            Role = Roles.Donor,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };
        _users[userId] = created;
        await EnsureRoleAsync(userId, Roles.Donor, ct);
        return Clone(created);
    }

    public Task<IReadOnlyList<DonorPreset>> ListDonorPresetsAsync(string userId, CancellationToken ct)
    {
        if (!_presets.TryGetValue(userId, out var list))
        {
            return Task.FromResult<IReadOnlyList<DonorPreset>>([]);
        }

        return Task.FromResult<IReadOnlyList<DonorPreset>>(list.Select(ClonePreset).ToList());
    }

    public Task<IReadOnlyList<DonorPreset>> ReplaceDonorPresetsAsync(
        string userId, IEnumerable<DonorPreset> presets, CancellationToken ct)
    {
        var updated = DonorPresetUtils.NormalizeForStorage(userId, presets);
        _presets[userId] = updated.Select(ClonePreset).ToList();
        return Task.FromResult<IReadOnlyList<DonorPreset>>(updated);
    }

    public async Task<IReadOnlyList<DonorPreset>> DeleteDonorPresetAsync(
        string userId, string restaurantName, string orderUrl, CancellationToken ct)
    {
        var list = await ListDonorPresetsAsync(userId, ct);
        var target = DonorPresetUtils.KeyFromPair(restaurantName, orderUrl);
        var next = list.Where(p => DonorPresetUtils.KeyForPreset(p) != target).ToList();
        return await ReplaceDonorPresetsAsync(userId, next, ct);
    }

    private static UserDto Clone(UserDto u) =>
        JsonSerializer.Deserialize<UserDto>(JsonSerializer.Serialize(u))!;

    private static DonorPreset ClonePreset(DonorPreset p) =>
        JsonSerializer.Deserialize<DonorPreset>(JsonSerializer.Serialize(p))!;
}
