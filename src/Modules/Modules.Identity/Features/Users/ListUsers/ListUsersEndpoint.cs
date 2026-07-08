using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Users.CreateUser;

namespace Modules.Identity.Features.Users.ListUsers;

public static class ListUsersEndpoint
{
    public static IEndpointRouteBuilder MapListUsers(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/users", async (
                Guid? brandId,
                Guid? branchId,
                ClaimsPrincipal principal,
                ListUsersQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var requester = RequestingUserContext.FromPrincipal(principal);
                var result = await handler.HandleAsync(requester, brandId, branchId, cancellationToken);

                return result.IsForbidden ? Results.StatusCode(StatusCodes.Status403Forbidden) : Results.Ok(result.Users);
            })
            .RequireAuthorization(AuthorizationPolicies.RequireBranchManagerOrAbove)
            .WithName("ListUsers")
            .WithTags("Users")
            .Produces<IReadOnlyList<UserResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
