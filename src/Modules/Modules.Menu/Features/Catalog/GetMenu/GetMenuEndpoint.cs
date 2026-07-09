using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Modules.Menu.Features.Catalog.GetMenu;

public static class GetMenuEndpoint
{
    /// <summary>
    /// Registered by the Identity module at the host level; referenced by
    /// name so Modules.Menu doesn't take a project reference on Identity.
    /// </summary>
    private const string RequireStaffOrAbove = "RequireStaffOrAbove";

    public static IEndpointRouteBuilder MapGetMenu(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/menu", async (
                ClaimsPrincipal user,
                GetMenuQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                // The brand always comes from the token, never the client —
                // same trust model as staff login. Corporate users carry no
                // brandId claim: the POS menu only makes sense in a
                // brand-scoped session.
                var brandClaim = user.FindFirstValue("brandId");
                if (!Guid.TryParse(brandClaim, out var brandId))
                {
                    return Results.Forbid();
                }

                return Results.Ok(await handler.HandleAsync(brandId, cancellationToken));
            })
            .RequireAuthorization(RequireStaffOrAbove)
            .WithName("GetMenu")
            .WithTags("Menu")
            .Produces<MenuResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
