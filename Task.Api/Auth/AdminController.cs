using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Task.Core;
using Task.Core.Auth;

namespace Task.Api.Auth;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class AdminController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly Database _database;

    public AdminController(IAuthService authService, Database database)
    {
        _authService = authService;
        _database = database;
    }

    public sealed record AdminUserResponse(string Id, string Username, bool IsAdmin, DateTime CreatedAt, DateTime? DisabledAt, int ApiKeyCount);
    public sealed record AdminTaskResponse(string Uid, string Title, string? Status, string? Assignee, string? UserId, DateTime CreatedAt, DateTime UpdatedAt);
    public sealed record AdminKeyResponse(string Id, string UserId, string Name, DateTime CreatedAt, DateTime? LastUsedAt, DateTime? RevokedAt);

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _authService.GetAllUsersAsync();
        var keys = await _authService.GetAllApiKeysAsync();
        var keyCounts = keys
            .GroupBy(k => k.UserId)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var dtos = users.Select(u => new AdminUserResponse(
            u.Id,
            u.Username,
            u.IsAdmin,
            u.CreatedAt,
            u.DisabledAt,
            keyCounts.TryGetValue(u.Id, out var count) ? count : 0)).ToList();

        return Ok(dtos);
    }

    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasks()
    {
        var tasks = await _database.GetAllTasksAdminAsync();
        var dtos = tasks.Select(t => new AdminTaskResponse(
            t.Uid, t.Title, t.Status, t.Assignee, t.UserId, t.CreatedAt, t.UpdatedAt)).ToList();
        return Ok(dtos);
    }

    [HttpGet("keys")]
    public async Task<IActionResult> GetKeys()
    {
        var keys = await _authService.GetAllApiKeysAsync();
        var dtos = keys.Select(k => new AdminKeyResponse(k.Id, k.UserId, k.Name, k.CreatedAt, k.LastUsedAt, k.RevokedAt)).ToList();
        return Ok(dtos);
    }

    [HttpPost("keys/{id}/revoke")]
    public async Task<IActionResult> RevokeKey(string id)
    {
        var revoked = await _authService.RevokeApiKeyAsync(id);
        return revoked ? NoContent() : NotFound();
    }
}

public static class AdminPolicy
{
    public const string Name = "Admin";
}
