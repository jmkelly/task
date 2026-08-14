using System;
using System.Collections.Generic;
using System.Threading;
using Task.Core.Auth;

namespace Task.Core;

public interface IAuthService
{
    /// <summary>Creates a user via self-registration. First user ever becomes admin.</summary>
    System.Threading.Tasks.Task<UserRecord?> SignupAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>Verifies credentials; returns the user or null. Disabled users never authenticate.</summary>
    System.Threading.Tasks.Task<UserRecord?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task<UserRecord?> FindUserByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Creates a user directly (CLI `task users create`, closed-signup setups).</summary>
    System.Threading.Tasks.Task<UserRecord> CreateUserAsync(string username, string password, bool isAdmin, CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task<List<UserRecord>> GetAllUsersAsync(CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task<ApiKeyRecord> CreateApiKeyAsync(string userId, string name, CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task<List<ApiKeyRecord>> GetApiKeysForUserAsync(string userId, CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task<List<ApiKeyRecord>> GetAllApiKeysAsync(CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task<ApiKeyRecord?> FindApiKeyByHashAsync(string keyHash, CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task UpdateApiKeyLastUsedAsync(string keyId, CancellationToken cancellationToken = default);

    /// <summary>Revokes a key. When userId is set, only that user's keys can be revoked. Idempotent.</summary>
    System.Threading.Tasks.Task<bool> RevokeApiKeyAsync(string keyId, string? userId = null, CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task SetUserDisabledAsync(string userId, DateTime? disabledAt, CancellationToken cancellationToken = default);
}

public sealed class AuthService : IAuthService
{
    private readonly Database _database;

    public AuthService(string dbPath)
        : this(DatabaseConnectionSettings.ForSqlite(dbPath))
    {
    }

    public AuthService(DatabaseConnectionSettings settings)
    {
        _database = new Database(settings);
    }

    public AuthService(Database database)
    {
        _database = database;
    }

    public async System.Threading.Tasks.Task<UserRecord?> SignupAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        ValidateUsername(username);
        ValidatePassword(password);

        var passwordHash = PasswordHasher.Hash(password);
        var result = await _database.CreateUserAndClaimTasksAsync(
            Uid.NewId(),
            username.Trim(),
            passwordHash,
            forceIsAdmin: false,
            cancellationToken);

        return result?.User;
    }

    public async System.Threading.Tasks.Task<UserRecord?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || password == null)
        {
            return null;
        }

        var user = await _database.FindUserByUsernameAsync(username.Trim(), cancellationToken);
        if (user == null || user.DisabledAt != null)
        {
            return null;
        }

        return PasswordHasher.Verify(password, user.PasswordHash) ? user : null;
    }

    public async System.Threading.Tasks.Task<UserRecord?> FindUserByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _database.FindUserByIdAsync(id, cancellationToken);
    }

    public async System.Threading.Tasks.Task<UserRecord> CreateUserAsync(string username, string password, bool isAdmin, CancellationToken cancellationToken = default)
    {
        ValidateUsername(username);
        ValidatePassword(password);

        var passwordHash = PasswordHasher.Hash(password);
        var result = await _database.CreateUserAndClaimTasksAsync(
            Uid.NewId(),
            username.Trim(),
            passwordHash,
            forceIsAdmin: isAdmin,
            cancellationToken);

        return result?.User
            ?? throw new InvalidOperationException($"Username '{username.Trim()}' is already taken.");
    }

    public async System.Threading.Tasks.Task<List<UserRecord>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _database.GetAllUsersAsync(cancellationToken);
    }

    public async System.Threading.Tasks.Task<ApiKeyRecord> CreateApiKeyAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        var plaintext = ApiKeyGenerator.Generate();
        var record = await _database.InsertApiKeyAsync(
            Uid.NewId(),
            userId,
            string.IsNullOrWhiteSpace(name) ? "default" : name.Trim(),
            ApiKeyGenerator.Hash(plaintext),
            cancellationToken);

        // The plaintext key is not stored anywhere; the caller must surface it exactly once.
        record.PlaintextKey = plaintext;
        return record;
    }

    public async System.Threading.Tasks.Task<List<ApiKeyRecord>> GetApiKeysForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _database.GetApiKeysForUserAsync(userId, cancellationToken);
    }

    public async System.Threading.Tasks.Task<List<ApiKeyRecord>> GetAllApiKeysAsync(CancellationToken cancellationToken = default)
    {
        return await _database.GetAllApiKeysAsync(cancellationToken);
    }

    public async System.Threading.Tasks.Task<ApiKeyRecord?> FindApiKeyByHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        return await _database.FindApiKeyByHashAsync(keyHash, cancellationToken);
    }

    public async System.Threading.Tasks.Task UpdateApiKeyLastUsedAsync(string keyId, CancellationToken cancellationToken = default)
    {
        await _database.UpdateApiKeyLastUsedAsync(keyId, cancellationToken);
    }

    public async System.Threading.Tasks.Task<bool> RevokeApiKeyAsync(string keyId, string? userId = null, CancellationToken cancellationToken = default)
    {
        return await _database.RevokeApiKeyAsync(keyId, userId, cancellationToken);
    }

    public async System.Threading.Tasks.Task SetUserDisabledAsync(string userId, DateTime? disabledAt, CancellationToken cancellationToken = default)
    {
        await _database.SetUserDisabledAsync(userId, disabledAt, cancellationToken);
    }

    internal static void ValidateUsername(string username)
    {
        var normalized = username?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 3 || normalized.Length > 32)
        {
            throw new ArgumentException("Username must be between 3 and 32 characters.");
        }

        foreach (var c in normalized)
        {
            var valid = (c >= 'a' && c <= 'z')
                || (c >= 'A' && c <= 'Z')
                || (c >= '0' && c <= '9')
                || c == '.'
                || c == '_'
                || c == '-';
            if (!valid)
            {
                throw new ArgumentException("Username may only contain letters, digits, '.', '_' and '-'.");
            }
        }
    }

    internal static void ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters.");
        }

        if (password.Length > 256)
        {
            throw new ArgumentException("Password must be at most 256 characters.");
        }
    }
}
