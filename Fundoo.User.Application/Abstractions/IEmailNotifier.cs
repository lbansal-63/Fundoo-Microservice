namespace Fundoo.User.Application.Abstractions;

public interface IEmailNotifier
{
    Task SendPasswordResetEmailAsync(string toEmail, string firstName, string token, CancellationToken ct = default);
    Task SendWelcomeEmailAsync(string toEmail, string firstName, CancellationToken ct = default);
}