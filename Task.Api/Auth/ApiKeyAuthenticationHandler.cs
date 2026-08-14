using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Task.Core;
using Task.Core.Auth;

namespace Task.Api.Auth;

/// <summary>
/// Authenticates CLI/AI-agent requests via the X-Api-Key header. The key itself is
/// never stored or logged: only its SHA-256 hash is looked up in the api_keys table.
/// Revoked keys and disabled users are rejected on every request.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";

    private readonly IAuthService _authService;
    private readonly ConcurrentDictionary<string, DateTime> _lastUsedWriteTimes = new(StringComparer.Ordinal);

    public ApiKeyAuthenticationHandler(
        IAuthService authService,
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        _authService = authService;
    }

    protected override async System.Threading.Tasks.Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var headerValues))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKey = headerValues.ToString();
        if (!ApiKeyGenerator.IsValidFormat(apiKey))
        {
            return AuthenticateResult.Fail("Invalid API key format.");
        }

        var keyHash = ApiKeyGenerator.Hash(apiKey);
        var key = await _authService.FindApiKeyByHashAsync(keyHash, Context.RequestAborted);
        if (key == null || key.RevokedAt != null)
        {
            return AuthenticateResult.Fail("Unknown or revoked API key.");
        }

        var user = await _authService.FindUserByIdAsync(key.UserId, Context.RequestAborted);
        if (user == null || user.DisabledAt != null)
        {
            return AuthenticateResult.Fail("API key owner is disabled.");
        }

        UpdateLastUsedOpportunistically(key.Id);

        var principal = AuthClaims.ToPrincipal(user, SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    /// <summary>
    /// The default authorization policy authenticates both the cookie scheme and this
    /// one. When a browser request fails, the cookie scheme already produced a redirect
    /// (or a 401 for /api/* and htmx) — never overwrite it with a bare 401.
    /// </summary>
    protected override System.Threading.Tasks.Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (Response.HasStarted || Response.StatusCode != StatusCodes.Status200OK)
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }

        return base.HandleChallengeAsync(properties);
    }

    /// <summary>
    /// Updates last_used_at at most once per key per few minutes — never on the
    /// per-request hot path. Failures are swallowed; the column is informational.
    /// </summary>
    private void UpdateLastUsedOpportunistically(string keyId)
    {
        var now = DateTime.UtcNow;
        if (_lastUsedWriteTimes.TryGetValue(keyId, out var lastWrite) && now - lastWrite < TimeSpan.FromMinutes(5))
        {
            return;
        }

        _lastUsedWriteTimes[keyId] = now;
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                await _authService.UpdateApiKeyLastUsedAsync(keyId);
            }
            catch
            {
                // Opportunistic bookkeeping; never fail a request over it.
            }
        });
    }
}

public static class ApiKeyAuthenticationExtensions
{
    public static AuthenticationBuilder AddApiKeyAuthentication(this AuthenticationBuilder builder)
    {
        return builder.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationHandler.SchemeName,
            configureOptions: null,
            displayName: "API key authentication (X-Api-Key)");
    }
}
