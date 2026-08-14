using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Task.Core;
using Task.Core.Auth;

namespace Task.Api.Pages;

[AllowAnonymous]
public class SignupModel : PageModel
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;

    public SignupModel(IAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
    }

    [BindProperty]
    public string? Username { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!_configuration.GetValue<bool>("Auth:AllowSignup", true))
        {
            ErrorMessage = "Self-registration is disabled on this server. Ask an admin to create your account.";
            return Page();
        }

        try
        {
            var user = await _authService.SignupAsync(Username ?? string.Empty, Password ?? string.Empty);
            if (user == null)
            {
                ErrorMessage = "That username is already taken.";
                return Page();
            }

            var principal = AuthClaims.ToPrincipal(user, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            return RedirectToPage("/Index");
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }
}
