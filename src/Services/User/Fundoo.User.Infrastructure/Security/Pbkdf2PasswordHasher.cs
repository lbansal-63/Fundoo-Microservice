using System.Security.Cryptography;
using Fundoo.User.Application.Abstractions;

namespace Fundoo.User.Infrastructure.Security;

/// <summary>
/// Implements RFC 2898 PBKDF2 password hashing with 100,000 iterations and SHA-256.
/// Also provides backwards-compatibility verification for legacy BCrypt hashes.
/// </summary>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;  // 128 bit salt
    private const int KeySize = 32;   // 256 bit subkey
    private const int Iterations = 100000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;

        // Backward compatibility for existing BCrypt hashes (e.g. $2a$ / $2b$)
        if (storedHash.StartsWith("$2a$") || storedHash.StartsWith("$2b$") || storedHash.StartsWith("$2y$"))
        {
            try { return BCrypt.Net.BCrypt.Verify(password, storedHash); }
            catch { return false; }
        }

        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;

        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expectedHash = Convert.FromBase64String(parts[1]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}
