namespace Fundoo.User.Domain.Entities;

public class AppUser
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public string? GoogleSubject { get; set; }

    // 2FA (TOTP)
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorSecret { get; set; }          // AES-GCM encrypted
    public long TwoFactorLastStep { get; set; }           // replay protection
    public string? RecoveryCodeHashes { get; set; }       // JSON array of hashes
    public int TwoFactorFailedCount { get; set; }
    public DateTime? TwoFactorLockedUntilUtc { get; set; }
}