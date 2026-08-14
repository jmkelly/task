using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Task.Core;
using Task.Core.Auth;

namespace Task.Api.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;

    public AuthController(IAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
    }

    public sealed record SignupRequest(string Username, string Password);
    public sealed record LoginRequest(string Username, string Password);
    public sealed record UserResponse(string Id, string Username, bool IsAdmin, DateTime CreatedAt);

    [HttpPost("signup")]
    [AllowAnonymous]
    public async Task<IActionResult> Signup([FromBody] SignupRequest request)
    {
        var allowSignup = _configuration.GetValue<bool>("Auth:AllowSignup", true);
        if (!allowSignup)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Self-registration is disabled on this server. Ask an admin to create your account." });
        }

        try
        {
            var user = await _authService.SignupAsync(request.Username, request.Password);
            if (user == null)
            {
                return Conflict(new { message = "That username is already taken." });
            }

            return StatusCode(StatusCodes.Status201Created, new UserResponse(user.Id, user.Username, user.IsAdmin, user.CreatedAt));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _authService.AuthenticateAsync(request.Username, request.Password);
        if (user == null)
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var principal = AuthClaims.ToPrincipal(user, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return Ok(new UserResponse(user.Id, user.Username, user.IsAdmin, user.CreatedAt));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("logout")]
    public async Task<IActionResult> LogoutGet()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/login");
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var userId = AuthClaims.GetUserId(User) ?? string.Empty;
        var username = AuthClaims.GetUsername(User) ?? string.Empty;
        return Ok(new UserResponse(userId, username, AuthClaims.IsAdminUser(User), DateTime.UtcNow));
    }
}
