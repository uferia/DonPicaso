using FluentValidation;

namespace Modules.Identity.Features.Brands.CreateBrand;

public sealed class CreateBrandCommandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
    }
}
