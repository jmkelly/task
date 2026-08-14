using System.Text.Json;

namespace Task.Api.Auth;

/// <summary>
/// Turns bare 401 responses on /api/* into JSON bodies with an actionable message
/// (keys-page URL, CLI remediation). Writes only when nothing else already wrote a body.
/// </summary>
public sealed class UnauthorizedBodyMiddleware
{
    private readonly RequestDelegate _next;

    public UnauthorizedBodyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async System.Threading.Tasks.Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.Response.StatusCode != StatusCodes.Status401Unauthorized)
        {
            return;
        }

        if (context.Response.HasStarted || !string.IsNullOrEmpty(context.Response.ContentType))
        {
            return;
        }

        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
        var message =
            "Authentication required. Sign in at /login or provide an API key via the X-Api-Key header. " +
            $"Create or manage API keys at {baseUrl}/keys.";

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { message }));
    }
}
