using FluentValidation;
using Fundoo.Notes.Api.Contracts;

namespace Fundoo.Notes.Api.Validators;

public class CreateNoteRequestValidator : AbstractValidator<CreateNoteRequest>
{
    public CreateNoteRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Content).NotNull().MaximumLength(20000);   // NotNull: JSON mein null aaye to service tak na jaye
        RuleFor(x => x.Color).MaximumLength(50).When(x => x.Color is not null);
    }
}

public class UpdateNoteRequestValidator : AbstractValidator<UpdateNoteRequest>
{
    public UpdateNoteRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Content).NotNull().MaximumLength(20000);
        RuleFor(x => x.Color).MaximumLength(50).When(x => x.Color is not null);
    }
}

public class LabelRequestValidator : AbstractValidator<LabelRequest>
{
    public LabelRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}