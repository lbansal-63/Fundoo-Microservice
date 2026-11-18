using System.Text.Json;
using Confluent.Kafka;
using Fundoo.User.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Fundoo.User.Infrastructure.Messaging;

public class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string PasswordResetTopic { get; set; } = "fundoo.password-reset-requested";
    public string UserRegisteredTopic { get; set; } = "fundoo.user-registered";
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
}

public record PasswordResetRequestedEvent(string Email, string FirstName, string Token, DateTime OccurredAtUtc);
public record UserRegisteredEvent(string Email, string FirstName, DateTime OccurredAtUtc);

public class KafkaEmailNotifier(
    IProducer<string, string> producer,
    KafkaOptions options,
    ILogger<KafkaEmailNotifier> logger) : IEmailNotifier
{
    public Task SendPasswordResetEmailAsync(string toEmail, string firstName, string token, CancellationToken ct = default) =>
        PublishAsync(options.PasswordResetTopic, toEmail,
            new PasswordResetRequestedEvent(toEmail, firstName, token, DateTime.UtcNow), ct);

    public Task SendWelcomeEmailAsync(string toEmail, string firstName, CancellationToken ct = default) =>
        PublishAsync(options.UserRegisteredTopic, toEmail,
            new UserRegisteredEvent(toEmail, firstName, DateTime.UtcNow), ct);

    private async Task PublishAsync<T>(string topic, string key, T evt, CancellationToken ct)
    {
        try
        {
            var result = await producer.ProduceAsync(topic,
                new Message<string, string> { Key = key, Value = JsonSerializer.Serialize(evt) }, ct);

            logger.LogInformation("Event published to {Topic} (partition {Partition}, offset {Offset})",
                topic, result.Partition.Value, result.Offset.Value);
        }
        catch (ProduceException<string, string> ex)
        {
            logger.LogError(ex, "Could not publish event to {Topic}", topic);
        }
    }
}