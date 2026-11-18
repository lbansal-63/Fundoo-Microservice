using System.Text;
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
    public string UserRegisteredTopic { get; set; } = "fundoo.user-registered";
    public string GroupId { get; set; } = "notification-service";

    public string DeadLetterSuffix { get; set; } = ".dlq";
    public int MaxAttempts { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 2000;

    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
}

public record PasswordResetRequestedEvent(string Email, string FirstName, string Token, DateTime OccurredAtUtc);
public record UserRegisteredEvent(string Email, string FirstName, DateTime OccurredAtUtc);

public enum HandleOutcome { Done, Permanent, Transient }

public class KafkaEmailConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<KafkaEmailConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(() => RunAsync(stoppingToken), stoppingToken);

    private async Task RunAsync(CancellationToken ct)
    {
        var o = options.Value;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = o.BootstrapServers,
            GroupId = o.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };
        var dlqConfig = new ProducerConfig
        {
            BootstrapServers = o.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = 5000
        };

        if (!string.IsNullOrWhiteSpace(o.ApiKey))
        {
            consumerConfig.SecurityProtocol = SecurityProtocol.SaslSsl;
            consumerConfig.SaslMechanism = SaslMechanism.Plain;
            consumerConfig.SaslUsername = o.ApiKey;
            consumerConfig.SaslPassword = o.ApiSecret;

            dlqConfig.SecurityProtocol = SecurityProtocol.SaslSsl;
            dlqConfig.SaslMechanism = SaslMechanism.Plain;
            dlqConfig.SaslUsername = o.ApiKey;
            dlqConfig.SaslPassword = o.ApiSecret;
        }

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        using var dlq = new ProducerBuilder<string, string>(dlqConfig).Build();

        consumer.Subscribe(new[] { o.PasswordResetTopic, o.UserRegisteredTopic });
        logger.LogInformation("Listening on topics {Topics}", string.Join(", ", o.PasswordResetTopic, o.UserRegisteredTopic));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var cr = consumer.Consume(ct);
                if (cr?.Message is null) continue;

                var outcome = await HandleWithRetryAsync(cr.Topic, cr.Message.Value, o, ct);

                if (outcome != HandleOutcome.Done)
                {
                    if (!await TrySendToDeadLetterAsync(dlq, cr, outcome, o, ct))
                    {
                        // DLQ bhi nahi mila: commit nahi, wapas isi offset par
                        consumer.Seek(cr.TopicPartitionOffset);
                        await Task.Delay(TimeSpan.FromSeconds(5), ct);
                        continue;
                    }
                }

                consumer.Commit(cr);
            }
            catch (OperationCanceledException) { break; }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume error");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in consumer loop");
            }
        }

        consumer.Close();
    }

    private async Task<HandleOutcome> HandleWithRetryAsync(string topic, string json, KafkaOptions o, CancellationToken ct)
    {
        var attempts = Math.Max(1, o.MaxAttempts);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            HandleOutcome outcome;
            try { outcome = await HandleAsync(topic, json, o, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Handler threw (topic {Topic}, attempt {Attempt}/{Max})", topic, attempt, attempts);
                outcome = HandleOutcome.Transient;
            }

            if (outcome != HandleOutcome.Transient) return outcome;

            if (attempt < attempts)
            {
                logger.LogWarning("Transient failure on {Topic}, attempt {Attempt}/{Max}; retrying", topic, attempt, attempts);
                await Task.Delay(o.RetryDelayMs * attempt, ct);
            }
        }
        return HandleOutcome.Transient;
    }

    private async Task<HandleOutcome> HandleAsync(string topic, string json, KafkaOptions o, CancellationToken ct)
    {
        if (topic == o.PasswordResetTopic)
        {
            var evt = TryParse<PasswordResetRequestedEvent>(json);
            if (evt is null) return HandleOutcome.Permanent;

            var body = $"Hi {evt.FirstName},\n\n" +
                       "Use the token below to reset your Fundoo password. It is valid for 15 minutes.\n\n" +
                       $"{evt.Token}\n\n" +
                       "If you did not request this, you can ignore this email.";
            return await SendAsync(evt.Email, "Fundoo password reset", body, ct);
        }

        if (topic == o.UserRegisteredTopic)
        {
            var evt = TryParse<UserRegisteredEvent>(json);
            if (evt is null) return HandleOutcome.Permanent;

            var body = $"Hi {evt.FirstName},\n\nYour Fundoo account is ready. Happy note-taking!";
            return await SendAsync(evt.Email, "Welcome to Fundoo", body, ct);
        }

        logger.LogWarning("Message from unexpected topic {Topic}", topic);
        return HandleOutcome.Permanent;
    }

    private async Task<HandleOutcome> SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var result = await notifications.SendEmailAsync(to, subject, body, false, ct);
        switch (result)
        {
            case SendEmailResult.Sent:
                logger.LogInformation("Email sent: {Subject}", subject);
                return HandleOutcome.Done;
            case SendEmailResult.DeliveryFailed:
                logger.LogWarning("Email delivery failed: {Subject}", subject);
                return HandleOutcome.Transient;
            default:
                logger.LogWarning("Email rejected as invalid ({Result}): {Subject}", result, subject);
                return HandleOutcome.Permanent;
        }
    }

    private T? TryParse<T>(string json) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(json); }
        catch (JsonException)
        {
            logger.LogWarning("Malformed message for {Type}", typeof(T).Name);
            return null;
        }
    }

    private async Task<bool> TrySendToDeadLetterAsync(
        IProducer<string, string> dlq, ConsumeResult<string, string> cr, HandleOutcome outcome, KafkaOptions o, CancellationToken ct)
    {
        var dlqTopic = cr.Topic + o.DeadLetterSuffix;
        try
        {
            await dlq.ProduceAsync(dlqTopic, new Message<string, string>
            {
                Key = cr.Message.Key,
                Value = cr.Message.Value,
                Headers = new Headers
                {
                    { "x-original-topic", Encoding.UTF8.GetBytes(cr.Topic) },
                    { "x-original-partition", Encoding.UTF8.GetBytes(cr.Partition.Value.ToString()) },
                    { "x-original-offset", Encoding.UTF8.GetBytes(cr.Offset.Value.ToString()) },
                    { "x-failure-reason", Encoding.UTF8.GetBytes(outcome.ToString()) }
                }
            }, ct);

            logger.LogError("Message moved to {DlqTopic} (original {Topic}[{Partition}]@{Offset}, reason {Reason})",
                dlqTopic, cr.Topic, cr.Partition.Value, cr.Offset.Value, outcome);
            return true;
        }
        catch (ProduceException<string, string> ex)
        {
            logger.LogError(ex, "Could not write to {DlqTopic}", dlqTopic);
            return false;
        }
    }
}