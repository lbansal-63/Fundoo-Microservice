using Fundoo.User.Application.Abstractions;
using Fundoo.User.Application.Auth;
using Fundoo.User.Infrastructure.Persistence;
using Fundoo.User.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Fundoo.User.Infrastructure.Notifications;
using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;

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
        var notifyBaseUrl = config["Notification:BaseUrl"]
        ?? throw new InvalidOperationException("Notification:BaseUrl is missing.");
        var notifyKey = config["Notification:ApiKey"]
            ?? throw new InvalidOperationException("Notification:ApiKey is missing.");

        services.AddHttpClient<IEmailNotifier, NotificationApiEmailNotifier>(c =>
        {
            c.BaseAddress = new Uri(notifyBaseUrl);
            c.DefaultRequestHeaders.Add("X-Internal-Key", notifyKey);
            c.Timeout = TimeSpan.FromSeconds(20);          // TOTAL timeout (saare retries milakar)
        })
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,                      // REDIRECT: chupchap follow nahi karna
        ConnectTimeout = TimeSpan.FromSeconds(3)        // TIMEOUT: connection banne ka time
    })
    .AddResilienceHandler("notification-pipeline", pipeline =>
    {
        // RETRY + JITTER
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 2,                       // pehli call + 2 retry = kul 3
            Delay = TimeSpan.FromMilliseconds(500),
            BackoffType = DelayBackoffType.Exponential, // 0.5s, 1s ...
            UseJitter = true,                           // har wait mein random time
            ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Exception is HttpRequestException ||
                args.Outcome.Result?.StatusCode is HttpStatusCode.BadGateway
                                                or HttpStatusCode.ServiceUnavailable)
        });

        // TIMEOUT: ek koshish ka time (retry ke andar)
        pipeline.AddTimeout(TimeSpan.FromSeconds(5));
    });
        services.AddScoped<IResetTokenService, JwtResetTokenService>();
        services.AddScoped<IForgotPasswordService, ForgotPasswordService>();

        return services;
    }
}