using FluentValidation;

namespace Modules.Identity.Features.Users.UpdateUser;

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(c => c.DisplayName).NotEmpty().MaximumLength(200);

        When(c => c.Role == UserRole.Staff, () =>
        {
            RuleFor(c => c.BrandId).NotNull();
            RuleFor(c => c.BranchId).NotNull();
            RuleFor(c => c.NewPin).Length(4).When(c => c.NewPin is not null);
        }).Otherwise(() =>
        {
            RuleFor(c => c.NewPassword).NotEmpty().When(c => c.NewPassword is not null);
        });
    }
}
