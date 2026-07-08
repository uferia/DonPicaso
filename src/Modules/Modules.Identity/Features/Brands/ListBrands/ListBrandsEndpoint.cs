using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Brands.CreateBrand;

namespace Modules.Identity.Features.Brands.ListBrands;

public static class ListBrandsEndpoint
{
    public static IEndpointRouteBuilder MapListBrands(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/brands", async (ListBrandsQueryHandler handler, CancellationToken cancellationToken) =>
                Results.Ok(await handler.HandleAsync(cancellationToken)))
            .RequireAuthorization(AuthorizationPolicies.RequireCorporate)
            .WithName("ListBrands")
            .WithTags("Brands")
            .Produces<IReadOnlyList<BrandResult>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
