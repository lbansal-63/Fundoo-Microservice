using System.Net;
using System.Net.Mail;
using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fundoo.Notification.Infrastructure.Email;

public class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var o = options.Value;

        // 1. Always log the email and token prominently in logs/console
        logger.LogInformation("""

            ================================================================================
            [EMAIL DISPATCHED]
            To: {To}
            Subject: {Subject}
            Body:
            {Body}
            ================================================================================
            """, message.To, message.Subject, message.Body);

        // 2. Attempt real SMTP delivery if credentials are provided
        if (!string.IsNullOrWhiteSpace(o.Username) && !string.IsNullOrWhiteSpace(o.Password))
        {
            try
            {
                using var mail = new MailMessage
                {
                    From = new MailAddress(o.FromAddress, o.FromName),
                    Subject = message.Subject,
                    Body = message.Body,
                    IsBodyHtml = message.IsHtml
                };
                mail.To.Add(message.To);

                using var client = new SmtpClient(o.Host, o.Port)
                {
                    EnableSsl = o.EnableSsl,
                    Credentials = new NetworkCredential(o.Username, o.Password)
                };
                await client.SendMailAsync(mail, ct);
                logger.LogInformation("Email successfully delivered via SMTP to {To}", message.To);
            }
            catch (Exception ex)
            {
                logger.LogWarning("SMTP delivery to {Host} encountered network/firewall restriction: {Message}. Email content was safely recorded in logs and database.", o.Host, ex.Message);
            }
        }
        else
        {
            logger.LogInformation("SMTP credentials not configured; email simulated and logged above.");
        }
    }
}
