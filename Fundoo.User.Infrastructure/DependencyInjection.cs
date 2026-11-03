using Confluent.Kafka;
using Fundoo.User.Application.Abstractions;
using Fundoo.User.Application.Auth;
using Fundoo.User.Infrastructure.Messaging;
using Fundoo.User.Infrastructure.Persistence;
using Fundoo.User.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fundoo.User.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUserInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var jwt = config.GetSection("Jwt").Get<JwtOptions>()
                  ?? throw new InvalidOperationException("Jwt section is missing.");
        if (jwt.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");

        var connectionString = config.GetConnectionString("UserDb")
                  ?? throw new InvalidOperationException("ConnectionStrings:UserDb is missing.");

        services.AddDbContext<UserDbContext>(o =>
            o.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(UserDbContext).Assembly.FullName)));

        services.AddSingleton(jwt);
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        // ---- Kafka producer (password reset event publish karne ke liye) ----
        var kafka = config.GetSection("Kafka").Get<KafkaOptions>() ?? new KafkaOptions();
        services.AddSingleton(kafka);

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = 5000
        };

        // Sirf cloud Kafka ke liye; local mein ApiKey khali rakho
        if (!string.IsNullOrWhiteSpace(kafka.ApiKey))
        {
            producerConfig.SecurityProtocol = SecurityProtocol.SaslSsl;
            producerConfig.SaslMechanism = SaslMechanism.Plain;
            producerConfig.SaslUsername = kafka.ApiKey;
            producerConfig.SaslPassword = kafka.ApiSecret;
        }

        services.AddSingleton<IProducer<string, string>>(_ =>
            new ProducerBuilder<string, string>(producerConfig).Build());

        services.AddScoped<IEmailNotifier, KafkaEmailNotifier>();
        // ----------------------------------------------------------------------

        services.AddScoped<IResetTokenService, JwtResetTokenService>();
        services.AddScoped<IForgotPasswordService, ForgotPasswordService>();

        return services;
    }
}