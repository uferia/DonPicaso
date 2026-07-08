using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Branches.CreateBranch;

namespace Modules.Identity.Features.Branches.UpdateBranch;

public static class UpdateBranchEndpoint
{
    public static IEndpointRouteBuilder MapUpdateBranch(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/v1/brands/{brandId:guid}/branches/{branchId:guid}", async (
                Guid brandId,
                Guid branchId,
                UpdateBranchCommand command,
                IValidator<UpdateBranchCommand> validator,
                UpdateBranchCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var validation = await validator.ValidateAsync(command, cancellationToken);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary());
                }

                var result = await handler.HandleAsync(brandId, branchId, command, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .RequireAuthorization(AuthorizationPolicies.RequireBrandOwnerOrAboveForOwnBrand)
            .WithName("UpdateBranch")
            .WithTags("Branches")
            .Produces<BranchResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem();

        return app;
    }
}
