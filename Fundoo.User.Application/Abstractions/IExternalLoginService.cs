using Fundoo.User.Application.Auth;

namespace Fundoo.User.Application.Abstractions;

public interface IExternalLoginService
{
    Task<AuthResult> LoginWithGoogleAsync(string idToken, CancellationToken ct = default);
}