using Confluent.Kafka;
using Fundoo.User.Application.Abstractions;
using Fundoo.User.Application.Auth;
using Fundoo.User.Infrastructure.Messaging;
using Fundoo.User.Infrastructure.Notifications;
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

        // ---- 2FA (TOTP) ----
        var twoFactor = config.GetSection("TwoFactor").Get<TwoFactorOptions>() ?? new TwoFactorOptions();
        var totpOptions = new TotpOptions { Issuer = config["TwoFactor:Issuer"] ?? "Fundoo" };
        services.AddSingleton(twoFactor);
        services.AddSingleton(totpOptions);
        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddScoped<ITwoFactorChallengeService, JwtTwoFactorChallengeService>();
        services.AddScoped<ITwoFactorService, TwoFactorService>();

        // ---- SSO (Google) ----
        var google = config.GetSection("Google").Get<GoogleOptions>() ?? new GoogleOptions();
        services.AddSingleton(google);
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<IExternalLoginService, ExternalLoginService>();

        // ---- Notification transport: "Kafka" (default) ya "Http" (OAuth client_credentials) ----
        var transport = config["Notification:Transport"] ?? "Kafka";
        if (transport.Equals("Http", StringComparison.OrdinalIgnoreCase))
        {
            var serviceAuth = config.GetSection("ServiceAuth").Get<ServiceAuthOptions>() ?? new ServiceAuthOptions();
            if (string.IsNullOrWhiteSpace(serviceAuth.TokenEndpoint) || string.IsNullOrWhiteSpace(serviceAuth.ClientId)
                || string.IsNullOrWhiteSpace(serviceAuth.ClientSecret))
                throw new InvalidOperationException(
                    "ServiceAuth:TokenEndpoint, ClientId and ClientSecret are required when Notification:Transport is Http.");

            var baseUrl = config["Notification:BaseUrl"]
                          ?? throw new InvalidOperationException("Notification:BaseUrl is missing.");

            services.AddSingleton(serviceAuth);
            services.AddHttpClient(ClientCredentialsTokenProvider.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(5));
            services.AddSingleton<ClientCredentialsTokenProvider>();
            services.AddTransient<ClientCredentialsHandler>();

            services.AddHttpClient<IEmailNotifier, NotificationApiEmailNotifier>(c =>
            {
                c.BaseAddress = new Uri(baseUrl);
                c.Timeout = TimeSpan.FromSeconds(5);
            })
                .AddHttpMessageHandler<ClientCredentialsHandler>();
        }
        else
        {
            var kafka = config.GetSection("Kafka").Get<KafkaOptions>() ?? new KafkaOptions();
            services.AddSingleton(kafka);

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = kafka.BootstrapServers,
                Acks = Acks.All,
                EnableIdempotence = true,
                MessageTimeoutMs = 5000
            };

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
        }

        services.AddScoped<IResetTokenService, JwtResetTokenService>();
        services.AddScoped<IForgotPasswordService, ForgotPasswordService>();

        return services;
    }
}