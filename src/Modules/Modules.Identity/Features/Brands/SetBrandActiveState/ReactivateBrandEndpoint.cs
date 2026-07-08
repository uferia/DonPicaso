using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Brands.CreateBrand;

namespace Modules.Identity.Features.Brands.SetBrandActiveState;

public static class ReactivateBrandEndpoint
{
    public static IEndpointRouteBuilder MapReactivateBrand(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/brands/{brandId:guid}/reactivate", async (
                Guid brandId, SetBrandActiveStateCommandHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(brandId, isActive: true, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .RequireAuthorization(AuthorizationPolicies.RequireCorporate)
            .WithName("ReactivateBrand")
            .WithTags("Brands")
            .Produces<BrandResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
