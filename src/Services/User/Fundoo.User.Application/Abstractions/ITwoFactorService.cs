using Fundoo.User.Application.Auth;

namespace Fundoo.User.Application.Abstractions;

public interface ITwoFactorService
{
    Task<TwoFactorSetupResult> SetupAsync(Guid userId, CancellationToken ct = default);
    Task<RecoveryCodesResult> EnableAsync(Guid userId, string code, CancellationToken ct = default);
    Task<AuthResult> DisableAsync(Guid userId, string? password, string code, CancellationToken ct = default);
    Task<AuthResult> CompleteLoginAsync(string challengeToken, string code, CancellationToken ct = default);
}