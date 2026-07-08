using FluentValidation;

namespace Modules.Identity.Features.Users.ResetCredential;

public sealed class ResetCredentialCommandValidator : AbstractValidator<ResetCredentialCommand>
{
    public ResetCredentialCommandValidator()
    {
        RuleFor(c => c.NewPin).Length(4).When(c => c.NewPin is not null);
        RuleFor(c => c.NewPassword).NotEmpty().When(c => c.NewPassword is not null);
    }
}
