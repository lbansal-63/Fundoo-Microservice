using System.Security.Cryptography;
using System.Text;
using Fundoo.User.Application.Abstractions;

namespace Fundoo.User.Infrastructure.Security;

public class TwoFactorOptions
{
    public string EncryptionKey { get; set; } = "";   // base64 of 32 bytes
}

public class AesGcmSecretProtector : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public AesGcmSecretProtector(TwoFactorOptions options)
    {
        try { _key = Convert.FromBase64String(options.EncryptionKey); }
        catch (FormatException) { throw new InvalidOperationException("TwoFactor:EncryptionKey must be base64."); }

        if (_key.Length != 32)
            throw new InvalidOperationException("TwoFactor:EncryptionKey must decode to exactly 32 bytes.");
    }

    public string Protect(string plainText)
    {
        var plain = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        var result = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, NonceSize);
        cipher.CopyTo(result, NonceSize + TagSize);
        return Convert.ToBase64String(result);
    }

    public string Unprotect(string protectedText)
    {
        var data = Convert.FromBase64String(protectedText);
        if (data.Length < NonceSize + TagSize)
            throw new CryptographicException("Protected value is too short.");

        var nonce = data.AsSpan(0, NonceSize);
        var tag = data.AsSpan(NonceSize, TagSize);
        var cipher = data.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}