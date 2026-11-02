using FluentValidation;
using Fundoo.Notification.Api.Contracts;

namespace Fundoo.Notification.Api.Validators;

public class SendEmailRequestValidator : AbstractValidator<SendEmailRequest>
{
    public SendEmailRequestValidator()
    {
        RuleFor(x => x.To).NotEmpty().EmailAddress().MaximumLength(256);

        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200)
            .Must(s => s is null || (!s.Contains('\r') && !s.Contains('\n')))
            .WithMessage("Subject must not contain line breaks.");   // header injection se bachav

        RuleFor(x => x.Body).NotEmpty().MaximumLength(50000);
    }
}