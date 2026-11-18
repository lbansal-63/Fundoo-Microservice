using System.Security.Cryptography;
using System.Text.Json;
using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entities;

namespace Fundoo.User.Application.Auth;

public class TwoFactorService(
    IUserRepository users,
    IPasswordHasher hasher,
    ITotpService totp,
    ISecretProtector protector,
    ITokenService tokens,
    ITwoFactorChallengeService challenges) : ITwoFactorService
{
    private const int MaxFailures = 5;
    private const int RecoveryCodeCount = 8;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<TwoFactorSetupResult> SetupAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId, ct);
        if (user is null || !user.IsActive) return TwoFactorSetupResult.Fail("Invalid request.");
        if (user.TwoFactorEnabled) return TwoFactorSetupResult.Fail("Two-factor authentication is already enabled.");

        var secret = totp.GenerateSecret();
        user.TwoFactorSecret = protector.Protect(secret);
        user.TwoFactorLastStep = 0;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await users.SaveChangesAsync(ct);

        return TwoFactorSetupResult.Ok(secret, totp.BuildOtpAuthUri(user.Email, secret));
    }

    public async Task<RecoveryCodesResult> EnableAsync(Guid userId, string code, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId, ct);
        if (user is null || !user.IsActive) return RecoveryCodesResult.Fail("Invalid request.");
        if (user.TwoFactorEnabled) return RecoveryCodesResult.Fail("Two-factor authentication is already enabled.");
        if (string.IsNullOrEmpty(user.TwoFactorSecret)) return RecoveryCodesResult.Fail("Start the setup first.");
        if (IsLocked(user)) return RecoveryCodesResult.Fail("Too many attempts. Try again later.");

        var secret = protector.Unprotect(user.TwoFactorSecret);
        if (!totp.Verify(secret, code.Trim(), user.TwoFactorLastStep, out var step))
        {
            await RegisterFailureAsync(user, ct);
            return RecoveryCodesResult.Fail("Invalid code.");
        }

        var codes = Enumerable.Range(0, RecoveryCodeCount).Select(_ => NewRecoveryCode()).ToList();

        user.TwoFactorEnabled = true;
        user.TwoFactorLastStep = step;
        user.RecoveryCodeHashes = JsonSerializer.Serialize(codes.Select(hasher.Hash).ToList());
        user.TwoFactorFailedCount = 0;
        user.TwoFactorLockedUntilUtc = null;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await users.SaveChangesAsync(ct);

        return RecoveryCodesResult.Ok(codes);
    }

    public async Task<AuthResult> DisableAsync(Guid userId, string? password, string code, CancellationToken ct = default)
    {
        const string invalid = "Invalid password or code.";

        var user = await users.FindByIdAsync(userId, ct);
        if (user is null || !user.IsActive) return AuthResult.Failure("Invalid request.");
        if (!user.TwoFactorEnabled) return AuthResult.Failure("Two-factor authentication is not enabled.");
        if (IsLocked(user)) return AuthResult.Failure("Too many attempts. Try again later.");

        if (!string.IsNullOrEmpty(user.PasswordHash)
            && (string.IsNullOrEmpty(password) || !hasher.Verify(password, user.PasswordHash)))
        {
            await RegisterFailureAsync(user, ct);
            return AuthResult.Failure(invalid);
        }

        if (!await VerifySecondFactorAsync(user, code, ct)) return AuthResult.Failure(invalid);

        ClearTwoFactor(user);
        user.UpdatedAtUtc = DateTime.UtcNow;
        await users.SaveChangesAsync(ct);
        return AuthResult.Success();
    }

    public async Task<AuthResult> CompleteLoginAsync(string challengeToken, string code, CancellationToken ct = default)
    {
        var userId = await challenges.ValidateAsync(challengeToken);
        if (userId is null) return AuthResult.Failure("Invalid or expired token.");

        var user = await users.FindByIdAsync(userId.Value, ct);
        if (user is null || !user.IsActive || !user.TwoFactorEnabled)
            return AuthResult.Failure("Invalid or expired token.");

        if (IsLocked(user)) return AuthResult.Failure("Too many attempts. Try again later.");

        if (!await VerifySecondFactorAsync(user, code, ct)) return AuthResult.Failure("Invalid code.");

        return AuthResult.Success(tokens.CreateAccessToken(user));
    }

    // ---------- helpers ----------

    private async Task<bool> VerifySecondFactorAsync(AppUser user, string code, CancellationToken ct)
    {
        code = code.Trim();
        var ok = false;

        if (code.Length == 6 && code.All(c => c is >= '0' and <= '9'))
        {
            var secret = protector.Unprotect(user.TwoFactorSecret!);
            if (totp.Verify(secret, code, user.TwoFactorLastStep, out var step))
            {
                user.TwoFactorLastStep = step;
                ok = true;
            }
        }
        else
        {
            ok = TryConsumeRecoveryCode(user, code);
        }

        if (ok)
        {
            user.TwoFactorFailedCount = 0;
            user.TwoFactorLockedUntilUtc = null;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await users.SaveChangesAsync(ct);
        }
        else
        {
            await RegisterFailureAsync(user, ct);
        }

        return ok;
    }

    private bool TryConsumeRecoveryCode(AppUser user, string input)
    {
        if (string.IsNullOrEmpty(user.RecoveryCodeHashes)) return false;

        var hashes = JsonSerializer.Deserialize<List<string>>(user.RecoveryCodeHashes) ?? new List<string>();
        var normalized = input.ToUpperInvariant();

        var index = hashes.FindIndex(h => hasher.Verify(normalized, h));
        if (index < 0) return false;

        hashes.RemoveAt(index);                                   // single use
        user.RecoveryCodeHashes = JsonSerializer.Serialize(hashes);
        return true;
    }

    private async Task RegisterFailureAsync(AppUser user, CancellationToken ct)
    {
        user.TwoFactorFailedCount++;
        if (user.TwoFactorFailedCount >= MaxFailures)
        {
            user.TwoFactorLockedUntilUtc = DateTime.UtcNow.Add(LockoutDuration);
            user.TwoFactorFailedCount = 0;
        }
        user.UpdatedAtUtc = DateTime.UtcNow;
        await users.SaveChangesAsync(ct);
    }

    private static bool IsLocked(AppUser user) =>
        user.TwoFactorLockedUntilUtc is { } until && until > DateTime.UtcNow;

    private static void ClearTwoFactor(AppUser user)
    {
        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.TwoFactorLastStep = 0;
        user.RecoveryCodeHashes = null;
        user.TwoFactorFailedCount = 0;
        user.TwoFactorLockedUntilUtc = null;
    }

    private static string NewRecoveryCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var chars = new char[11];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = i == 5 ? '-' : alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(chars);
    }
}