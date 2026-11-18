using System.Net.Http.Json;
using Fundoo.User.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Polly.RateLimiting;
using Polly.Timeout;

namespace Fundoo.User.Infrastructure.Notifications;

public class NotificationApiEmailNotifier(HttpClient http, ILogger<NotificationApiEmailNotifier> logger) : IEmailNotifier
{
    public Task SendPasswordResetEmailAsync(string toEmail, string firstName, string token, CancellationToken ct = default) =>
        SendAsync(toEmail, "Fundoo password reset",
            $"Hi {firstName},\n\n" +
            "Use the token below to reset your Fundoo password. It is valid for 15 minutes.\n\n" +
            $"{token}\n\n" +
            "If you did not request this, you can ignore this email.", ct);

    public Task SendWelcomeEmailAsync(string toEmail, string firstName, CancellationToken ct = default) =>
        SendAsync(toEmail, "Welcome to Fundoo",
            $"Hi {firstName},\n\nYour Fundoo account is ready. Happy note-taking!", ct);

    private async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("api/notifications/email",
                new { to = toEmail, subject, body, isHtml = false }, ct);

            if (response.IsSuccessStatusCode)
                logger.LogInformation("Email request accepted by Notification service");
            else
                logger.LogWarning("Notification service returned {StatusCode}", (int)response.StatusCode);
        }
        catch (RateLimiterRejectedException)
        {
            logger.LogWarning("Notification calls limit (bulkhead) reached; email request was skipped");
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException or TimeoutRejectedException)
                                   && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Notification service could not be reached, timed out, or token fetch failed");
        }
    }
}