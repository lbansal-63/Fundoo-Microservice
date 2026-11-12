using Fundoo.User.Application.Abstractions;
using OtpNet;

namespace Fundoo.User.Infrastructure.Security;

public class TotpOptions
{
    public string Issuer { get; set; } = "Fundoo";
}

public class TotpService(TotpOptions options) : ITotpService
{
    public string GenerateSecret() =>
        Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));

    public string BuildOtpAuthUri(string email, string secret)
    {
        var issuer = Uri.EscapeDataString(options.Issuer);
        var label = Uri.EscapeDataString($"{options.Issuer}:{email}");
        return $"otpauth://totp/{label}?secret={secret}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";
    }

    public bool Verify(string secret, string code, long lastUsedStep, out long matchedStep)
    {
        matchedStep = 0;
        var totp = new Totp(Base32Encoding.ToBytes(secret));

        if (!totp.VerifyTotp(code, out var step, new VerificationWindow(previous: 1, future: 1)))
            return false;

        if (step <= lastUsedStep) return false;   // replay

        matchedStep = step;
        return true;
    }
}