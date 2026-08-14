using System;
using System.Security.Claims;

namespace Task.Core.Auth;

/// <summary>
/// Claim names and helpers for the shared identity model used by both the
/// cookie-session scheme (browser) and the X-Api-Key scheme (CLI/agents).
/// </summary>
public static class AuthClaims
{
    public const string UserId = "task_user_id";
    public const string Username = "task_username";
    public const string IsAdmin = "task_is_admin";

    public static ClaimsPrincipal ToPrincipal(UserRecord user, string authenticationScheme)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(AuthClaims.UserId, user.Id),
            new Claim(AuthClaims.Username, user.Username),
            new Claim(AuthClaims.IsAdmin, user.IsAdmin ? "true" : "false")
        };

        var identity = new ClaimsIdentity(claims, authenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    public static string? GetUserId(ClaimsPrincipal principal)
    {
        return principal.FindFirst(AuthClaims.UserId)?.Value;
    }

    public static string? GetUsername(ClaimsPrincipal principal)
    {
        return principal.FindFirst(AuthClaims.Username)?.Value;
    }

    public static bool IsAdminUser(ClaimsPrincipal principal)
    {
        return string.Equals(principal.FindFirst(AuthClaims.IsAdmin)?.Value, "true", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// A user account row. Passwords and keys are never exposed in plaintext.
/// </summary>
public sealed class UserRecord
{
    public required string Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsAdmin { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? DisabledAt { get; set; }
}

/// <summary>
/// An API key row. KeyHash is the SHA-256 of the "tk_..." key; plaintext is never stored.
/// PlaintextKey is set transiently on creation and must never be persisted.
/// </summary>
public sealed class ApiKeyRecord
{
    public required string Id { get; set; }
    public required string UserId { get; set; }
    public required string Name { get; set; }
    public required string KeyHash { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? PlaintextKey { get; set; }
}

/// <summary>Read-only admin view of a task row (any owner, including unowned legacy rows).</summary>
public sealed class AdminTaskView
{
    public required string Uid { get; set; }
    public required string Title { get; set; }
    public required string Status { get; set; }
    public string? Assignee { get; set; }
    public string? UserId { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
}
