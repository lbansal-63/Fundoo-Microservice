using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entities;

namespace Fundoo.User.Application.Auth;

public class AuthService(
    IUserRepository users,
    IPasswordHasher hasher,
    ITokenService tokens,
    ITwoFactorChallengeService challenges,
    IEmailNotifier notifier) : IAuthService
{
    public async Task<AuthResult> RegisterAsync(string firstName, string lastName, string email, string password, CancellationToken ct = default)
    {
        var normalized = EmailNormalizer.Normalize(email);

        if (await users.FindByNormalizedEmailAsync(normalized, ct) is not null)
            return AuthResult.Failure("An account with this email already exists.");

        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email.Trim(),
            NormalizedEmail = normalized,
            PasswordHash = hasher.Hash(password),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await users.AddAsync(user, ct);
        await users.SaveChangesAsync(ct);

        await TrySendWelcomeAsync(user, ct);
        return AuthResult.Success();
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await users.FindByNormalizedEmailAsync(EmailNormalizer.Normalize(email), ct);

        // PasswordHash khali = sirf-Google account (BCrypt khali hash par exception deta hai)
        if (user is null || !user.IsActive
            || string.IsNullOrEmpty(user.PasswordHash)
            || !hasher.Verify(password, user.PasswordHash))
            return AuthResult.Failure("Invalid email or password.");

        if (user.TwoFactorEnabled)
            return AuthResult.TwoFactorRequired(challenges.Create(user));

        return AuthResult.Success(tokens.CreateAccessToken(user));
    }

    private async Task TrySendWelcomeAsync(AppUser user, CancellationToken ct)
    {
        try { await notifier.SendWelcomeEmailAsync(user.Email, user.FirstName, ct); }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Welcome email optional hai, registration fail nahi honi chahiye
        }
    }
}