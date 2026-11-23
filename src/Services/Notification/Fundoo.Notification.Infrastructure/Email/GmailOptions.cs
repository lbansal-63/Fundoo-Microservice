namespace Fundoo.Notification.Infrastructure.Email;

public class GmailOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "laksha.bansal7015@gmail.com";
    public string FromName { get; set; } = "Fundoo Notes";
}
