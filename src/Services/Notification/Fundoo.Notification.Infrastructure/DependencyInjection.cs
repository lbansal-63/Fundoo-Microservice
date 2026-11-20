using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Application.Emails;
using Fundoo.Notification.Infrastructure.Email;
using Fundoo.Notification.Infrastructure.Messaging;
using Fundoo.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fundoo.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("NotificationDb")
            ?? config.GetConnectionString("UserDb")
            ?? "Host=localhost;Port=5432;Database=fundoonotes_notification;Username=postgres;Password=postgres";

        services.AddDbContext<NotificationDbContext>(o =>
            o.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(NotificationDbContext).Assembly.FullName)));

        services.AddOptions<SmtpOptions>()
            .Bind(config.GetSection("Smtp"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Host) && !string.IsNullOrWhiteSpace(o.FromAddress),
                      "Smtp:Host and Smtp:FromAddress are required.")
            .ValidateOnStart();

        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<INotificationService, NotificationService>();

        services.Configure<KafkaOptions>(config.GetSection("Kafka"));
        services.AddHostedService<KafkaEmailConsumer>();

        return services;
    }
}