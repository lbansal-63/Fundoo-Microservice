using Fundoo.User.Domain.Entities;

namespace Fundoo.User.Application.Abstractions;

public interface ITwoFactorChallengeService
{
    string Create(AppUser user);
    Task<Guid?> ValidateAsync(string token);
}