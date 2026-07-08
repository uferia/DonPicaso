using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Branches.CreateBranch;

namespace Modules.Identity.Features.Branches.SetBranchActiveState;

public static class DeactivateBranchEndpoint
{
    public static IEndpointRouteBuilder MapDeactivateBranch(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/brands/{brandId:guid}/branches/{branchId:guid}/deactivate", async (
                Guid brandId, Guid branchId, SetBranchActiveStateCommandHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(brandId, branchId, isActive: false, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .RequireAuthorization(AuthorizationPolicies.RequireBrandOwnerOrAboveForOwnBrand)
            .WithName("DeactivateBranch")
            .WithTags("Branches")
            .Produces<BranchResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
