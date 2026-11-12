namespace Fundoo.User.Application.Abstractions;

public interface ITotpService
{
    string GenerateSecret();
    string BuildOtpAuthUri(string email, string secret);
    bool Verify(string secret, string code, long lastUsedStep, out long matchedStep);
}