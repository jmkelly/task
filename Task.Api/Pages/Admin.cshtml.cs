using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Task.Core;
using Task.Api.Auth;
using Task.Core;
using Task.Core.Auth;

namespace Task.Api.Pages;

[Authorize(Policy = AdminPolicy.Name)]
public class AdminModel : PageModel
{
    private readonly IAuthService _authService;
    private readonly Database _database;

    public AdminModel(IAuthService authService, Database database)
    {
        _authService = authService;
        _database = database;
    }

    public sealed record AdminUserRow(string Username, bool IsAdmin, DateTime CreatedAt, DateTime? DisabledAt, int ApiKeyCount);
    public sealed record AdminKeyRow(string Id, string OwnerUsername, string Name, DateTime CreatedAt, DateTime? RevokedAt);
    public sealed record AdminTaskRow(string Uid, string Title, string Status, string? Assignee, string? OwnerUsername);

    public List<AdminUserRow> Users { get; set; } = new();
    public List<AdminKeyRow> Keys { get; set; } = new();
    public List<AdminTaskRow> Tasks { get; set; } = new();
    public List<string> Owners { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? OwnerFilter { get; set; }

    public async System.Threading.Tasks.Task OnGetAsync()
    {
        var users = await _authService.GetAllUsersAsync();
        var userById = users.ToDictionary(u => u.Id, StringComparer.Ordinal);

        var allKeys = await _authService.GetAllApiKeysAsync();
        var keyCounts = allKeys
            .GroupBy(k => k.UserId)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Users = users
            .OrderBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
            .Select(u => new AdminUserRow(
                u.Username,
                u.IsAdmin,
                u.CreatedAt,
                u.DisabledAt,
                keyCounts.TryGetValue(u.Id, out var count) ? count : 0))
            .ToList();

        Keys = allKeys
            .OrderBy(k => k.CreatedAt)
            .Select(k => new AdminKeyRow(
                k.Id,
                userById.TryGetValue(k.UserId, out var owner) ? owner.Username : "unknown",
                k.Name,
                k.CreatedAt,
                k.RevokedAt))
            .ToList();

        var allTasks = await _database.GetAllTasksAdminAsync();
        var tasks = allTasks
            .Select(t => new AdminTaskRow(
                t.Uid,
                t.Title,
                t.Status,
                t.Assignee,
                t.UserId != null && userById.TryGetValue(t.UserId, out var owner) ? owner.Username : null))
            .ToList();
        Tasks = tasks;

        Owners = tasks
            .Where(t => t.OwnerUsername != null)
            .Select(t => t.OwnerUsername!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(o => o, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async System.Threading.Tasks.Task<IActionResult> OnPostRevokeKeyAsync(string id)
    {
        await _authService.RevokeApiKeyAsync(id);
        return RedirectToPage();
    }
}
