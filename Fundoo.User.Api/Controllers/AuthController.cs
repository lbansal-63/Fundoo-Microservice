using Fundoo.User.Api.Contracts;
using Fundoo.User.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace Fundoo.User.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController(
    IAuthService auth,
    IForgotPasswordService forgotPassword,
    ITwoFactorService twoFactor,
    IExternalLoginService external) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(
            request.FirstName, request.LastName, request.Email, request.Password, ct);

        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);

        return Ok(new { message = "Registration successful. You can now log in." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request.Email, request.Password, ct);
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: result.Error);

        if (result.RequiresTwoFactor)
            return Ok(new TwoFactorRequiredResponse(true, result.TwoFactorToken!));

        return Ok(new TokenResponse(result.AccessToken!));
    }

    [HttpPost("login/2fa")]
    public async Task<IActionResult> LoginTwoFactor(TwoFactorLoginRequest request, CancellationToken ct)
    {
        var result = await twoFactor.CompleteLoginAsync(request.TwoFactorToken, request.Code, ct);
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: result.Error);

        return Ok(new TokenResponse(result.AccessToken!));
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin(GoogleLoginRequest request, CancellationToken ct)
    {
        var result = await external.LoginWithGoogleAsync(request.IdToken, ct);
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: result.Error);

        return Ok(new TokenResponse(result.AccessToken!));
    }

    [HttpPost("2fa/setup")]
    [Authorize]
    public async Task<IActionResult> SetupTwoFactor(CancellationToken ct)
    {
        var result = await twoFactor.SetupAsync(UserId, ct);
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);

        return Ok(new TwoFactorSetupResponse(result.Secret!, result.OtpAuthUri!));
    }

    [HttpPost("2fa/enable")]
    [Authorize]
    public async Task<IActionResult> EnableTwoFactor(TwoFactorEnableRequest request, CancellationToken ct)
    {
        var result = await twoFactor.EnableAsync(UserId, request.Code, ct);
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);

        return Ok(new RecoveryCodesResponse(result.Codes!));
    }

    [HttpPost("2fa/disable")]
    [Authorize]
    public async Task<IActionResult> DisableTwoFactor(TwoFactorDisableRequest request, CancellationToken ct)
    {
        var result = await twoFactor.DisableAsync(UserId, request.Password, request.Code, ct);
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);

        return Ok(new { message = "Two-factor authentication has been disabled." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await forgotPassword.RequestAsync(request.Email, ct);
        return Ok(new { message = "If an account exists for this email, a reset token has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await forgotPassword.ResetAsync(request.Token, request.NewPassword, ct);
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);

        return Ok(new { message = "Password has been reset. You can now log in." });
    }
}