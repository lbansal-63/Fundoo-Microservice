using System.Text.Json;
using Confluent.Kafka;
using Fundoo.User.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Fundoo.User.Infrastructure.Messaging;

public class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string PasswordResetTopic { get; set; } = "fundoo.password-reset-requested";
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
}

public record PasswordResetRequestedEvent(string Email, string FirstName, string Token, DateTime OccurredAtUtc);

public class KafkaEmailNotifier(
    IProducer<string, string> producer,
    KafkaOptions options,
    ILogger<KafkaEmailNotifier> logger) : IEmailNotifier
{
    public async Task SendPasswordResetEmailAsync(string toEmail, string firstName, string token, CancellationToken ct = default)
    {
        var evt = new PasswordResetRequestedEvent(toEmail, firstName, token, DateTime.UtcNow);

        try
        {
            var result = await producer.ProduceAsync(options.PasswordResetTopic,
                new Message<string, string> { Key = toEmail, Value = JsonSerializer.Serialize(evt) }, ct);

            logger.LogInformation("Password reset event published (partition {Partition}, offset {Offset})",
                result.Partition.Value, result.Offset.Value);
        }
        catch (ProduceException<string, string> ex)
        {
            logger.LogWarning(ex, "Could not publish password reset event to Kafka");
        }
    }
}