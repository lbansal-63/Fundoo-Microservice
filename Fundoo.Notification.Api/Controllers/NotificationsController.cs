using Fundoo.Notification.Api.Contracts;
using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Application.Emails;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Fundoo.Notification.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize(Policy = "notification.send")]
public class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpPost("email")]
    public async Task<IActionResult> SendEmail(SendEmailRequest r, CancellationToken ct)
    {
        var result = await notifications.SendEmailAsync(r.To, r.Subject, r.Body, r.IsHtml, ct);

        return result switch
        {
            SendEmailResult.Sent => Accepted(),
            SendEmailResult.InvalidRecipient => Problem(statusCode: 400, title: "Invalid recipient address."),
            SendEmailResult.InvalidContent => Problem(statusCode: 400, title: "Invalid subject or body."),
            _ => Problem(statusCode: 502, title: "Email could not be delivered.")
        };
    }
}