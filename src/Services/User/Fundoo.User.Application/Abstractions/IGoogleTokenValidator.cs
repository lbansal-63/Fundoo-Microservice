namespace Fundoo.User.Application.Abstractions;

public record GoogleIdentity(string Subject, string Email, bool EmailVerified, string FirstName, string LastName);

public interface IGoogleTokenValidator
{
    bool IsConfigured { get; }
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken ct = default);
}