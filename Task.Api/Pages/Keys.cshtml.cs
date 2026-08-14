using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Task.Core;
using Task.Core.Auth;

namespace Task.Api.Pages;

[Authorize]
public class KeysModel : PageModel
{
    private readonly IAuthService _authService;

    public KeysModel(IAuthService authService)
    {
        _authService = authService;
    }

    public List<ApiKeyRecord> Keys { get; set; } = new();

    [TempData]
    public string? NewKeyPlaintext { get; set; }

    public async System.Threading.Tasks.Task OnGetAsync()
    {
        await LoadKeysAsync();
    }

    public async System.Threading.Tasks.Task<IActionResult> OnPostCreateAsync(string? name)
    {
        var userId = AuthClaims.GetUserId(User);
        if (userId == null)
        {
            return Challenge();
        }

        var key = await _authService.CreateApiKeyAsync(userId, string.IsNullOrWhiteSpace(name) ? "default" : name);
        NewKeyPlaintext = key.PlaintextKey;
        await LoadKeysAsync();
        return Page();
    }

    public async System.Threading.Tasks.Task<IActionResult> OnPostRevokeAsync(string id)
    {
        var userId = AuthClaims.GetUserId(User);
        if (userId == null)
        {
            return Challenge();
        }

        await _authService.RevokeApiKeyAsync(id, userId);
        await LoadKeysAsync();
        return Page();
    }

    private async System.Threading.Tasks.Task LoadKeysAsync()
    {
        var userId = AuthClaims.GetUserId(User);
        Keys = userId == null ? new List<ApiKeyRecord>() : await _authService.GetApiKeysForUserAsync(userId);
    }
}
