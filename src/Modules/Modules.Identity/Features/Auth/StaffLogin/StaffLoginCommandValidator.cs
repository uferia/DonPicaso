using FluentValidation;

namespace Modules.Identity.Features.Auth.StaffLogin;

public sealed class StaffLoginCommandValidator : AbstractValidator<StaffLoginCommand>
{
    public StaffLoginCommandValidator()
    {
        RuleFor(c => c.BranchId).NotEmpty();
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Pin).NotEmpty().Length(4);
    }
}
