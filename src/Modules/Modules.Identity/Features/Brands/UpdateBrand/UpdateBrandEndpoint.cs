using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Brands.CreateBrand;

namespace Modules.Identity.Features.Brands.UpdateBrand;

public static class UpdateBrandEndpoint
{
    public static IEndpointRouteBuilder MapUpdateBrand(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/v1/brands/{brandId:guid}", async (
                Guid brandId,
                UpdateBrandCommand command,
                IValidator<UpdateBrandCommand> validator,
                UpdateBrandCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var validation = await validator.ValidateAsync(command, cancellationToken);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary());
                }

                var result = await handler.HandleAsync(brandId, command, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .RequireAuthorization(AuthorizationPolicies.RequireCorporate)
            .WithName("UpdateBrand")
            .WithTags("Brands")
            .Produces<BrandResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem();

        return app;
    }
}
