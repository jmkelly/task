using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Task.Core;
using Task.Core.Auth;

namespace Task.Api.Auth;

[ApiController]
[Route("api/keys")]
[Authorize]
public class KeysController : ControllerBase
{
    private readonly IAuthService _authService;

    public KeysController(IAuthService authService)
    {
        _authService = authService;
    }

    public sealed record CreateKeyRequest(string Name);
    public sealed record KeyResponse(string Id, string Name, DateTime CreatedAt, DateTime? LastUsedAt, DateTime? RevokedAt, string? Key = null);

    [HttpGet]
    public async Task<IActionResult> GetKeys()
    {
        var userId = AuthClaims.GetUserId(User);
        if (userId == null)
        {
            return Unauthorized();
        }

        var keys = await _authService.GetApiKeysForUserAsync(userId);
        var dtos = keys.Select(k => new KeyResponse(k.Id, k.Name, k.CreatedAt, k.LastUsedAt, k.RevokedAt)).ToList();
        return Ok(dtos);
    }

    [HttpPost]
    public async Task<IActionResult> CreateKey([FromBody] CreateKeyRequest request)
    {
        var userId = AuthClaims.GetUserId(User);
        if (userId == null)
        {
            return Unauthorized();
        }

        var name = string.IsNullOrWhiteSpace(request.Name) ? "default" : request.Name.Trim();
        if (name.Length > 64)
        {
            return BadRequest(new { message = "Key name must be at most 64 characters." });
        }

        var key = await _authService.CreateApiKeyAsync(userId, name);

        // The plaintext key is returned exactly once, at creation. It is never stored.
        return StatusCode(StatusCodes.Status201Created, new KeyResponse(
            key.Id,
            key.Name,
            key.CreatedAt,
            key.LastUsedAt,
            key.RevokedAt,
            key.PlaintextKey));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> RevokeKey(string id)
    {
        var userId = AuthClaims.GetUserId(User);
        if (userId == null)
        {
            return Unauthorized();
        }

        var revoked = await _authService.RevokeApiKeyAsync(id, userId);
        return revoked ? NoContent() : NotFound();
    }
}
