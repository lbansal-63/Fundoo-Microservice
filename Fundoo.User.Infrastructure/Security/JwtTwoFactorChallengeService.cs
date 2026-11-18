using System.Security.Claims;
using System.Text;
using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Fundoo.User.Infrastructure.Security;

public class JwtTwoFactorChallengeService(JwtOptions options) : ITwoFactorChallengeService
{
    public const string ChallengeAudience = "fundoo-2fa-challenge";
    public const int ExpiryMinutes = 5;

    private SymmetricSecurityKey Key => new(Encoding.UTF8.GetBytes(options.SigningKey));

    public string Create(AppUser user)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim("purpose", "2fa-challenge")
            }),
            Issuer = options.Issuer,
            Audience = ChallengeAudience,
            Expires = DateTime.UtcNow.AddMinutes(ExpiryMinutes),
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public async Task<Guid?> ValidateAsync(string token)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = ChallengeAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = Key,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
        };

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
        if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt) return null;
        if (!jwt.TryGetPayloadValue<string>("purpose", out var purpose) || purpose != "2fa-challenge") return null;
        return Guid.TryParse(jwt.Subject, out var userId) ? userId : null;
    }
}