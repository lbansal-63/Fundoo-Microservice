namespace Fundoo.User.Application.Auth;

public record AuthResult(
    bool Succeeded,
    string? Error,
    string? AccessToken,
    bool RequiresTwoFactor = false,
    string? TwoFactorToken = null)
{
    public static AuthResult Success(string? accessToken = null) => new(true, null, accessToken);
    public static AuthResult Failure(string error) => new(false, error, null);
    public static AuthResult TwoFactorRequired(string challengeToken) => new(true, null, null, true, challengeToken);
}

public record TwoFactorSetupResult(bool Succeeded, string? Error, string? Secret, string? OtpAuthUri)
{
    public static TwoFactorSetupResult Ok(string secret, string uri) => new(true, null, secret, uri);
    public static TwoFactorSetupResult Fail(string error) => new(false, error, null, null);
}

public record RecoveryCodesResult(bool Succeeded, string? Error, IReadOnlyList<string>? Codes)
{
    public static RecoveryCodesResult Ok(IReadOnlyList<string> codes) => new(true, null, codes);
    public static RecoveryCodesResult Fail(string error) => new(false, error, null);
}