using Fundoo.User.Api.Contracts;
using Fundoo.User.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.User.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request.FirstName, request.LastName, request.Email, request.Password, ct);
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: result.Error);

        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request.Email, request.Password, ct);
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: result.Error);

        return Ok(new TokenResponse(result.AccessToken!));
    }
}