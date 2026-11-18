using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entities;

namespace Fundoo.User.UnitTests;

public class FakeUserRepository : IUserRepository
{
    public List<AppUser> Users { get; } = new();

    public Task<AppUser?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.NormalizedEmail == normalizedEmail));

    public Task<AppUser?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<AppUser?> FindByGoogleSubjectAsync(string googleSubject, CancellationToken ct = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.GoogleSubject == googleSubject));

    public Task AddAsync(AppUser user, CancellationToken ct = default)
    {
        Users.Add(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}

public class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "hashed:" + password;
    public bool Verify(string password, string hash) => hash == "hashed:" + password;
}

public class FakeTokenService : ITokenService
{
    public string CreateAccessToken(AppUser user) => "token-for-" + user.Id;
}

public class FakeTwoFactorChallengeService : ITwoFactorChallengeService
{
    public string Create(AppUser user) => "challenge-for-" + user.Id;

    public Task<Guid?> ValidateAsync(string token) =>
        Task.FromResult<Guid?>(null);
}

public class FakeEmailNotifier : IEmailNotifier
{
    public Task SendPasswordResetEmailAsync(string toEmail, string firstName, string token, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SendWelcomeEmailAsync(string toEmail, string firstName, CancellationToken ct = default) =>
        Task.CompletedTask;
}