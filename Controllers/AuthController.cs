using ClubManagement.Dtos;
using ClubManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClubManagement.Controllers;

[ApiController, Authorize, Route("api/auth")]
public sealed class AuthController(AuthService service, Actor actor) : ControllerBase
{
    [AllowAnonymous, EnableRateLimiting("AuthAttempts"), HttpPost("login")]
    public Task<TokenResponse> Login(LoginRequest request, CancellationToken ct) => service.LoginAsync(request, ct);

    [AllowAnonymous, EnableRateLimiting("AuthAttempts"), HttpPost("refresh")]
    public Task<TokenResponse> Refresh(RefreshRequest request, CancellationToken ct) => service.RefreshAsync(request, ct);

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await service.LogoutAsync(actor.SessionId, actor.Id, ct);
        return NoContent();
    }
}
