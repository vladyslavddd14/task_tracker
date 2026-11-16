using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Application.Dtos;
using TaskTracker.Application.Services;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var response = await auth.RegisterAsync(request, ct);
        return Created("/api/me", response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await auth.LoginAsync(request, ct));
}

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController(ProfileService profile) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserDto>> Get(CancellationToken ct) =>
        Ok(await profile.GetAsync(User.GetUserId(), ct));

    [HttpPut]
    public async Task<ActionResult<UserDto>> Update(UpdateProfileRequest request, CancellationToken ct) =>
        Ok(await profile.UpdateAsync(User.GetUserId(), request, ct));

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await profile.ChangePasswordAsync(User.GetUserId(), request, ct);
        return NoContent();
    }

    [HttpPost("apply-template")]
    public async Task<ActionResult<UserDto>> ApplyTemplate(ApplyTemplateRequest request, CancellationToken ct) =>
        Ok(await profile.ApplyTemplateAsync(User.GetUserId(), request, ct));
}
