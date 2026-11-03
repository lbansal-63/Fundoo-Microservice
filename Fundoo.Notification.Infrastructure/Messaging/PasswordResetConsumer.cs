using System.Text.Json;
using Confluent.Kafka;
using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Application.Emails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fundoo.Notification.Infrastructure.Messaging;

public class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string PasswordResetTopic { get; set; } = "fundoo.password-reset-requested";
    public string GroupId { get; set; } = "notification-service";
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
}

public record PasswordResetRequestedEvent(string Email, string FirstName, string Token, DateTime OccurredAtUtc);

public class PasswordResetConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<PasswordResetConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(() => RunAsync(stoppingToken), stoppingToken);

    private async Task RunAsync(CancellationToken ct)
    {
        var o = options.Value;
        var config = new ConsumerConfig
        {
            BootstrapServers = o.BootstrapServers,
            GroupId = o.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        if (!string.IsNullOrWhiteSpace(o.ApiKey))
        {
            config.SecurityProtocol = SecurityProtocol.SaslSsl;
            config.SaslMechanism = SaslMechanism.Plain;
            config.SaslUsername = o.ApiKey;
            config.SaslPassword = o.ApiSecret;
        }

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(o.PasswordResetTopic);
        logger.LogInformation("Listening on topic {Topic}", o.PasswordResetTopic);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var cr = consumer.Consume(ct);
                if (cr?.Message is null) continue;

                await HandleAsync(cr.Message.Value, ct);
                consumer.Commit(cr);
            }
            catch (OperationCanceledException) { break; }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume error");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while handling message");
            }
        }

        consumer.Close();
    }

    private async Task HandleAsync(string json, CancellationToken ct)
    {
        PasswordResetRequestedEvent? evt;
        try { evt = JsonSerializer.Deserialize<PasswordResetRequestedEvent>(json); }
        catch (JsonException)
        {
            logger.LogWarning("Skipping malformed message");
            return;
        }
        if (evt is null) return;

        var body = $"Hi {evt.FirstName},\n\n" +
                   "Use the token below to reset your Fundoo password. It is valid for 15 minutes.\n\n" +
                   $"{evt.Token}\n\n" +
                   "If you did not request this, you can ignore this email.";

        using var scope = scopeFactory.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var result = await notifications.SendEmailAsync(evt.Email, "Fundoo password reset", body, false, ct);

        if (result == SendEmailResult.Sent)
            logger.LogInformation("Password reset email sent");
        else
            logger.LogWarning("Password reset email not sent: {Result}", result);
    }
}