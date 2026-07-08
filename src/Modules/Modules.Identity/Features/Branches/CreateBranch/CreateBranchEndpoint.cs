using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;

namespace Modules.Identity.Features.Branches.CreateBranch;

public static class CreateBranchEndpoint
{
    public static IEndpointRouteBuilder MapCreateBranch(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/brands/{brandId:guid}/branches", async (
                Guid brandId,
                CreateBranchCommand command,
                IValidator<CreateBranchCommand> validator,
                CreateBranchCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var validation = await validator.ValidateAsync(command, cancellationToken);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary());
                }

                var result = await handler.HandleAsync(brandId, command, cancellationToken);
                return Results.Ok(result);
            })
            .RequireAuthorization(AuthorizationPolicies.RequireBrandOwnerOrAboveForOwnBrand)
            .WithName("CreateBranch")
            .WithTags("Branches")
            .Produces<BranchResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem();

        return app;
    }
}
