using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Branches.CreateBranch;

namespace Modules.Identity.Features.Branches.GetBranch;

public static class GetBranchEndpoint
{
    public static IEndpointRouteBuilder MapGetBranch(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/brands/{brandId:guid}/branches/{branchId:guid}", async (
                Guid brandId, Guid branchId, GetBranchQueryHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(brandId, branchId, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .RequireAuthorization(AuthorizationPolicies.RequireBrandOwnerOrAboveForOwnBrand)
            .WithName("GetBranch")
            .WithTags("Branches")
            .Produces<BranchResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
