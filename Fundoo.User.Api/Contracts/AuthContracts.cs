namespace Fundoo.User.Api.Contracts;

public class RegisterRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = "";
}

public class ResetPasswordRequest
{
    public string Token { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

public class TwoFactorLoginRequest
{
    public string TwoFactorToken { get; set; } = "";
    public string Code { get; set; } = "";
}

public class TwoFactorEnableRequest
{
    public string Code { get; set; } = "";
}

public class TwoFactorDisableRequest
{
    public string? Password { get; set; }
    public string Code { get; set; } = "";
}

public class GoogleLoginRequest
{
    public string IdToken { get; set; } = "";
}

public record TokenResponse(string AccessToken);
public record TwoFactorRequiredResponse(bool RequiresTwoFactor, string TwoFactorToken);
public record TwoFactorSetupResponse(string Secret, string OtpAuthUri);
public record RecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);