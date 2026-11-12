using Fundoo.User.Application.Abstractions;
using Google.Apis.Auth;

namespace Fundoo.User.Infrastructure.Security;

public class GoogleOptions
{
    public string ClientId { get; set; } = "";
}

public class GoogleTokenValidator(GoogleOptions options) : IGoogleTokenValidator
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ClientId);

    public async Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { options.ClientId } });

            return new GoogleIdentity(
                payload.Subject,
                payload.Email ?? "",
                payload.EmailVerified,
                payload.GivenName ?? "",
                payload.FamilyName ?? "");
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}