using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace SharingBridge.UserService;

public sealed class PostgresUserStore : IUserStore, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresUserStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public static async Task<PostgresUserStore> CreateAsync(
        string connectionString,
        DataAccessOptions? options = null,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("DATABASE_URL is required for PostgresUserStore.");
        }

        options ??= new DataAccessOptions();
        var normalized = DatabaseUrl.Normalize(connectionString, options);
        logger?.LogInformation("Postgres connection: {Conn}", DatabaseUrl.DescribeForLog(normalized));
        logger?.LogInformation("Data access options: {@Options}", options.ToPublicConfig());
        var dataSource = NpgsqlDataSource.Create(normalized);
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT 1", conn);
        await cmd.ExecuteScalarAsync(ct);
        return new PostgresUserStore(dataSource);
    }

    public async ValueTask DisposeAsync() => await _dataSource.DisposeAsync();

    public async Task<IReadOnlyList<string>> GetRolesForUserAsync(string userId, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        return await GetRolesOnConnectionAsync(conn, userId, ct);
    }

    public async Task EnsureRoleAsync(string userId, string role, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await EnsureRoleOnConnectionAsync(conn, userId, role, ct);
    }

    public async Task<UserDto> GetOrCreateUserAsync(string userId, string? phone, string? email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("userId is required.");
        }

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using (var existing = new NpgsqlCommand("SELECT * FROM users WHERE id = $1", conn))
        {
            existing.Parameters.AddWithValue(userId);
            await using var reader = await existing.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                await reader.DisposeAsync();
                await using var updated = new NpgsqlCommand(
                    """
                    UPDATE users SET
                      phone = COALESCE($2, phone),
                      email = COALESCE($3, email),
                      updated_at = now()
                    WHERE id = $1
                    RETURNING *
                    """, conn);
                updated.Parameters.AddWithValue(userId);
                updated.Parameters.AddWithValue((object?)NullIfEmpty(phone) ?? DBNull.Value);
                updated.Parameters.AddWithValue((object?)NullIfEmpty(email) ?? DBNull.Value);
                await using var updatedReader = await updated.ExecuteReaderAsync(ct);
                await updatedReader.ReadAsync(ct);
                var user = MapUser(updatedReader, Roles.Donor);
                await updatedReader.DisposeAsync();
                await EnsureRoleOnConnectionAsync(conn, userId, Roles.Donor, ct);
                var roles = await GetRolesOnConnectionAsync(conn, userId, ct);
                user.Role = roles.Contains(Roles.Coordinator) ? Roles.Coordinator : Roles.Donor;
                return user;
            }
        }

        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO users (id, phone, email, google_sub, name, picture)
            VALUES ($1, $2, $3, NULL, NULL, NULL)
            RETURNING *
            """, conn);
        insert.Parameters.AddWithValue(userId);
        insert.Parameters.AddWithValue((object?)NullIfEmpty(phone) ?? DBNull.Value);
        insert.Parameters.AddWithValue((object?)NullIfEmpty(email) ?? DBNull.Value);
        await using var insertReader = await insert.ExecuteReaderAsync(ct);
        await insertReader.ReadAsync(ct);
        var created = MapUser(insertReader, Roles.Donor);
        await insertReader.DisposeAsync();
        await EnsureRoleOnConnectionAsync(conn, userId, Roles.Donor, ct);
        return created;
    }

    public async Task<UserDto> FindOrCreateGoogleUserAsync(
        string googleSub, string? email, string? name, string? picture, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(googleSub))
        {
            throw new InvalidOperationException("googleSub is required.");
        }

        var sub = googleSub.Trim();
        // One connection for the whole sign-in path — avoids pooler exhaustion on Render free tier.
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using (var bySub = new NpgsqlCommand("SELECT * FROM users WHERE google_sub = $1", conn))
        {
            bySub.Parameters.AddWithValue(sub);
            await using var reader = await bySub.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                await reader.DisposeAsync();
                await using var updated = new NpgsqlCommand(
                    """
                    UPDATE users SET
                      email = COALESCE($2, email),
                      name = COALESCE($3, name),
                      picture = COALESCE($4, picture),
                      updated_at = now()
                    WHERE google_sub = $1
                    RETURNING *
                    """, conn);
                updated.Parameters.AddWithValue(sub);
                updated.Parameters.AddWithValue((object?)NullIfEmpty(email) ?? DBNull.Value);
                updated.Parameters.AddWithValue((object?)NullIfEmpty(name) ?? DBNull.Value);
                updated.Parameters.AddWithValue((object?)NullIfEmpty(picture) ?? DBNull.Value);
                await using var updatedReader = await updated.ExecuteReaderAsync(ct);
                await updatedReader.ReadAsync(ct);
                var user = MapUser(updatedReader, Roles.Donor);
                await updatedReader.DisposeAsync();
                await EnsureRoleOnConnectionAsync(conn, user.Id, Roles.Donor, ct);
                var roles = await GetRolesOnConnectionAsync(conn, user.Id, ct);
                user.Role = roles.Contains(Roles.Coordinator) ? Roles.Coordinator : Roles.Donor;
                return user;
            }
        }

        var userId = UserIdFromGoogleSub(sub);
        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO users (id, google_sub, email, name, picture, phone)
            VALUES ($1, $2, $3, $4, $5, NULL)
            RETURNING *
            """, conn);
        insert.Parameters.AddWithValue(userId);
        insert.Parameters.AddWithValue(sub);
        insert.Parameters.AddWithValue((object?)NullIfEmpty(email) ?? DBNull.Value);
        insert.Parameters.AddWithValue((object?)NullIfEmpty(name) ?? DBNull.Value);
        insert.Parameters.AddWithValue((object?)NullIfEmpty(picture) ?? DBNull.Value);
        await using var insertReader = await insert.ExecuteReaderAsync(ct);
        await insertReader.ReadAsync(ct);
        var created = MapUser(insertReader, Roles.Donor);
        await insertReader.DisposeAsync();
        await EnsureRoleOnConnectionAsync(conn, userId, Roles.Donor, ct);
        return created;
    }

    public async Task<IReadOnlyList<DonorPreset>> ListDonorPresetsAsync(string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT presets_json FROM donor_presets WHERE user_id = $1", conn);
        cmd.Parameters.AddWithValue(userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is null or DBNull)
        {
            return [];
        }

        var json = result is string s ? s : result.ToString() ?? "[]";
        var presets = JsonSerializer.Deserialize<List<DonorPreset>>(json) ?? [];
        return presets;
    }

    public async Task<IReadOnlyList<DonorPreset>> ReplaceDonorPresetsAsync(
        string userId, IEnumerable<DonorPreset> presets, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("userId is required.");
        }

        var updated = DonorPresetUtils.NormalizeForStorage(userId, presets);
        var json = JsonSerializer.Serialize(updated);
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO donor_presets (user_id, presets_json, updated_at)
            VALUES ($1, $2::jsonb, now())
            ON CONFLICT (user_id) DO UPDATE SET
              presets_json = EXCLUDED.presets_json,
              updated_at = now()
            """, conn);
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(json);
        await cmd.ExecuteNonQueryAsync(ct);
        return updated;
    }

    public async Task<IReadOnlyList<DonorPreset>> DeleteDonorPresetAsync(
        string userId, string restaurantName, string orderUrl, CancellationToken ct)
    {
        var list = await ListDonorPresetsAsync(userId, ct);
        var target = DonorPresetUtils.KeyFromPair(restaurantName, orderUrl);
        var next = list.Where(p => DonorPresetUtils.KeyForPreset(p) != target).ToList();
        return await ReplaceDonorPresetsAsync(userId, next, ct);
    }

    private static async Task EnsureRoleOnConnectionAsync(
        NpgsqlConnection conn, string userId, string role, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO user_roles (user_id, role) VALUES ($1, $2) ON CONFLICT DO NOTHING", conn);
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(role);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<IReadOnlyList<string>> GetRolesOnConnectionAsync(
        NpgsqlConnection conn, string userId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT role FROM user_roles WHERE user_id = $1 ORDER BY role", conn);
        cmd.Parameters.AddWithValue(userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var roles = new List<string>();
        while (await reader.ReadAsync(ct))
        {
            roles.Add(reader.GetString(0));
        }

        return roles;
    }

    private static string UserIdFromGoogleSub(string googleSub)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(googleSub));
        var hex = Convert.ToHexString(hash).ToLowerInvariant()[..16];
        return $"u_{hex}";
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static UserDto MapUser(NpgsqlDataReader reader, string activeRole)
    {
        var id = reader.GetString(reader.GetOrdinal("id"));
        var createdAt = reader.GetFieldValue<object>(reader.GetOrdinal("created_at"));
        string createdAtIso = createdAt switch
        {
            DateTime dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("O"),
            DateTimeOffset dto => dto.UtcDateTime.ToString("O"),
            _ => createdAt?.ToString() ?? ""
        };

        return new UserDto
        {
            Id = id,
            UserId = id,
            Phone = reader.IsDBNull(reader.GetOrdinal("phone")) ? null : reader.GetString(reader.GetOrdinal("phone")),
            Email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email")),
            Role = activeRole,
            GoogleSub = reader.IsDBNull(reader.GetOrdinal("google_sub")) ? null : reader.GetString(reader.GetOrdinal("google_sub")),
            Name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name")),
            Picture = reader.IsDBNull(reader.GetOrdinal("picture")) ? null : reader.GetString(reader.GetOrdinal("picture")),
            CreatedAt = createdAtIso
        };
    }
}
