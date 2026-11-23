using Consul;

namespace Fundoo.Notification.Api.Extensions;

/// <summary>
/// Consul ke saath Notification service register/deregister karne ke liye extension.
/// </summary>
public static class ConsulServiceRegistration
{
    public static IServiceCollection AddConsulRegistration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IConsulClient, ConsulClient>(sp =>
        {
            var host = configuration["Consul:Host"] ?? "localhost";
            var port = int.Parse(configuration["Consul:Port"] ?? "8500");
            return new ConsulClient(cfg =>
            {
                cfg.Address = new Uri($"http://{host}:{port}");
            });
        });

        return services;
    }

    public static IApplicationBuilder UseConsulRegistration(
        this IApplicationBuilder app,
        IConfiguration configuration,
        IHostApplicationLifetime lifetime)
    {
        var consulClient = app.ApplicationServices.GetRequiredService<IConsulClient>();
        var logger = app.ApplicationServices.GetRequiredService<ILogger<Program>>();

        var serviceName = configuration["Consul:ServiceName"] ?? "notification-api";
        var serviceId = configuration["Consul:ServiceId"] ?? $"notification-api-{Guid.NewGuid()}";
        var serviceAddress = configuration["Consul:ServiceAddress"] ?? "localhost";
        var servicePort = int.Parse(configuration["Consul:ServicePort"] ?? "7003");
        var scheme = configuration["Consul:Scheme"] ?? "https";

        var registration = new AgentServiceRegistration
        {
            ID = serviceId,
            Name = serviceName,
            Address = serviceAddress,
            Port = servicePort,
            Tags = new[] { "notification", "api", "v1" },
            Check = new AgentServiceCheck
            {
                HTTP = $"{scheme}://{serviceAddress}:{servicePort}/health",
                Interval = TimeSpan.FromSeconds(15),
                Timeout = TimeSpan.FromSeconds(5),
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1),
                TLSSkipVerify = true
            }
        };

        lifetime.ApplicationStarted.Register(async () =>
        {
            try
            {
                await consulClient.Agent.ServiceDeregister(serviceId);
                await consulClient.Agent.ServiceRegister(registration);
                logger.LogInformation("Notification API successfully registered with Consul. ServiceId={ServiceId}, Address={Address}:{Port}",
                    serviceId, serviceAddress, servicePort);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Consul service registration failed for Notification API: {Message}", ex.Message);
            }
        });

        lifetime.ApplicationStopping.Register(async () =>
        {
            try
            {
                await consulClient.Agent.ServiceDeregister(serviceId);
                logger.LogInformation("Notification API deregistered from Consul. ServiceId={ServiceId}", serviceId);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Consul service deregistration failed: {Message}", ex.Message);
            }
        });

        return app;
    }
}
