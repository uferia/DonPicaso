using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Branches.CreateBranch;

namespace Modules.Identity.Features.Branches.SetBranchActiveState;

public static class ReactivateBranchEndpoint
{
    public static IEndpointRouteBuilder MapReactivateBranch(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/brands/{brandId:guid}/branches/{branchId:guid}/reactivate", async (
                Guid brandId, Guid branchId, SetBranchActiveStateCommandHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(brandId, branchId, isActive: true, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .RequireAuthorization(AuthorizationPolicies.RequireBrandOwnerOrAboveForOwnBrand)
            .WithName("ReactivateBranch")
            .WithTags("Branches")
            .Produces<BranchResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
