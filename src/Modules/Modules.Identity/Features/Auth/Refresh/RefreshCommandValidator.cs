using FluentValidation;

namespace Modules.Identity.Features.Auth.Refresh;

public sealed class RefreshCommandValidator : AbstractValidator<RefreshCommand>
{
    public RefreshCommandValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty();
    }
}
