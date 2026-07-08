using FluentValidation;

namespace Modules.Identity.Features.Users.CreateUser;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(c => c.DisplayName).NotEmpty().MaximumLength(200);

        When(c => c.Role == UserRole.Staff, () =>
        {
            RuleFor(c => c.BrandId).NotNull();
            RuleFor(c => c.BranchId).NotNull();
            RuleFor(c => c.Pin).NotEmpty().Length(4);
        }).Otherwise(() =>
        {
            RuleFor(c => c.Email).NotEmpty().EmailAddress();
            RuleFor(c => c.Password).NotEmpty();
        });
    }
}
