using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Brands.CreateBrand;

namespace Modules.Identity.Features.Brands.GetBrand;

public static class GetBrandEndpoint
{
    public static IEndpointRouteBuilder MapGetBrand(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/brands/{brandId:guid}", async (
                Guid brandId, GetBrandQueryHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(brandId, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .RequireAuthorization(AuthorizationPolicies.RequireCorporate)
            .WithName("GetBrand")
            .WithTags("Brands")
            .Produces<BrandResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
