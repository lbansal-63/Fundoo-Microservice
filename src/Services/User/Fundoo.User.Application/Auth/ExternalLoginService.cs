using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entities;

namespace Fundoo.User.Application.Auth;

public class ExternalLoginService(
    IUserRepository users,
    IGoogleTokenValidator google,
    ITokenService tokens,
    IEmailNotifier notifier) : IExternalLoginService
{
    public async Task<AuthResult> LoginWithGoogleAsync(string idToken, CancellationToken ct = default)
    {
        const string invalid = "Invalid Google token.";

        if (!google.IsConfigured) return AuthResult.Failure("Google sign-in is not configured.");

        var identity = await google.ValidateAsync(idToken, ct);
        if (identity is null || !identity.EmailVerified || string.IsNullOrWhiteSpace(identity.Email))
            return AuthResult.Failure(invalid);

        var user = await users.FindByGoogleSubjectAsync(identity.Subject, ct);
        var isNew = false;

        if (user is null)
        {
            var normalized = EmailNormalizer.Normalize(identity.Email);
            user = await users.FindByNormalizedEmailAsync(normalized, ct);

            if (user is null)
            {
                var now = DateTime.UtcNow;
                user = new AppUser
                {
                    Id = Guid.NewGuid(),
                    FirstName = string.IsNullOrWhiteSpace(identity.FirstName) ? "User" : identity.FirstName.Trim(),
                    LastName = identity.LastName.Trim(),
                    Email = identity.Email.Trim(),
                    NormalizedEmail = normalized,
                    PasswordHash = "",
                    GoogleSubject = identity.Subject,
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                await users.AddAsync(user, ct);
                isNew = true;
            }
            else
            {
                if (!string.IsNullOrEmpty(user.GoogleSubject) && user.GoogleSubject != identity.Subject)
                    return AuthResult.Failure(invalid);

                // Link par purana password + 2FA hata do (pre-registration takeover se bachav)
                user.GoogleSubject = identity.Subject;
                user.PasswordHash = "";
                user.TwoFactorEnabled = false;
                user.TwoFactorSecret = null;
                user.TwoFactorLastStep = 0;
                user.RecoveryCodeHashes = null;
                user.TwoFactorFailedCount = 0;
                user.TwoFactorLockedUntilUtc = null;
                user.UpdatedAtUtc = DateTime.UtcNow;
            }

            await users.SaveChangesAsync(ct);
        }

        if (!user.IsActive) return AuthResult.Failure(invalid);

        if (isNew)
        {
            try { await notifier.SendWelcomeEmailAsync(user.Email, user.FirstName, ct); }
            catch (Exception) when (!ct.IsCancellationRequested) { }
        }

        return AuthResult.Success(tokens.CreateAccessToken(user));
    }
}