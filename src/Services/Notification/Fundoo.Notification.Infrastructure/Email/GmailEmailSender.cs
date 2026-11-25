using System.Text;
using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Domain.Entities;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Fundoo.Notification.Infrastructure.Email;

public class GmailEmailSender(
    IOptions<GmailOptions> gmailOptions,
    ILogger<GmailEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
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

        var g = gmailOptions.Value;

        // 2. If Gmail OAuth is configured, send via Gmail REST API (Port 443 HTTPS - Not blocked by Azure)
        if (!string.IsNullOrWhiteSpace(g.ClientId) &&
            !string.IsNullOrWhiteSpace(g.ClientSecret) &&
            !string.IsNullOrWhiteSpace(g.RefreshToken))
        {
            try
            {
                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new ClientSecrets
                    {
                        ClientId = g.ClientId,
                        ClientSecret = g.ClientSecret
                    },
                    Scopes = new[] { GmailService.Scope.MailGoogleCom }
                });

                var tokenResponse = new TokenResponse { RefreshToken = g.RefreshToken };
                var credential = new UserCredential(flow, "user", tokenResponse);

                // Auto refreshes the access token using refresh_token
                await credential.RefreshTokenAsync(ct);

                using var service = new GmailService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "FundooNotes"
                });

                var mimeMessage = new MimeMessage();
                mimeMessage.From.Add(new MailboxAddress(g.FromName, g.FromEmail));
                mimeMessage.To.Add(MailboxAddress.Parse(message.To));
                mimeMessage.Subject = message.Subject;

                var bodyBuilder = new BodyBuilder();
                if (message.IsHtml)
                {
                    bodyBuilder.HtmlBody = message.Body;
                }
                else
                {
                    bodyBuilder.TextBody = message.Body;
                }
                mimeMessage.Body = bodyBuilder.ToMessageBody();

                using var stream = new MemoryStream();
                await mimeMessage.WriteToAsync(stream, ct);
                var raw = Convert.ToBase64String(stream.ToArray())
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .Replace("=", "");

                var gmailMessage = new Message { Raw = raw };
                await service.Users.Messages.Send(gmailMessage, "me").ExecuteAsync(ct);

                logger.LogInformation(">>> [SUCCESS] Email delivered to {To} via Gmail OAuth REST API!", message.To);
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Gmail OAuth API failed: {Message}. Falling back...", ex.Message);
            }
        }

        logger.LogInformation("Gmail OAuth not configured or encountered error. Email logged in console above.");
    }
}
