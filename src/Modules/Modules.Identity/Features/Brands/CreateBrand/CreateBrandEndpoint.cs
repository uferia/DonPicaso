using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;

namespace Modules.Identity.Features.Brands.CreateBrand;

public static class CreateBrandEndpoint
{
    public static IEndpointRouteBuilder MapCreateBrand(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/brands", async (
                CreateBrandCommand command,
                IValidator<CreateBrandCommand> validator,
                CreateBrandCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var validation = await validator.ValidateAsync(command, cancellationToken);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary());
                }

                var result = await handler.HandleAsync(command, cancellationToken);
                return Results.Ok(result);
            })
            .RequireAuthorization(AuthorizationPolicies.RequireCorporate)
            .WithName("CreateBrand")
            .WithTags("Brands")
            .Produces<BrandResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem();

        return app;
    }
}
