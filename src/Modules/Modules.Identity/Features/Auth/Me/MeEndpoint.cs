using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;

namespace Modules.Identity.Features.Auth.Me;

public static class MeEndpoint
{
    public static IEndpointRouteBuilder MapMe(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/auth/me", (ClaimsPrincipal principal) =>
            {
                var userId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
                var role = principal.FindFirstValue("role")!;
                var brandId = principal.FindFirstValue("brandId") is { } b ? Guid.Parse(b) : (Guid?)null;
                var branchId = principal.FindFirstValue("branchId") is { } br ? Guid.Parse(br) : (Guid?)null;

                return Results.Ok(new MeResponse(userId, role, brandId, branchId));
            })
            .RequireAuthorization(AuthorizationPolicies.RequireStaffOrAbove)
            .WithName("GetCurrentUser")
            .WithTags("Auth")
            .Produces<MeResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}

public sealed record MeResponse(Guid UserId, string Role, Guid? BrandId, Guid? BranchId);
