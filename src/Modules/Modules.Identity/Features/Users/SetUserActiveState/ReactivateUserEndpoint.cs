using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Users.CreateUser;

namespace Modules.Identity.Features.Users.SetUserActiveState;

public static class ReactivateUserEndpoint
{
    public static IEndpointRouteBuilder MapReactivateUser(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/users/{userId:guid}/reactivate", async (
                Guid userId, ClaimsPrincipal principal, SetUserActiveStateCommandHandler handler, CancellationToken cancellationToken) =>
            {
                var requester = RequestingUserContext.FromPrincipal(principal);
                var result = await handler.HandleAsync(requester, userId, isActive: true, cancellationToken);

                return result.Error switch
                {
                    UserOperationError.None => Results.Ok(result.User),
                    UserOperationError.NotFound => Results.NotFound(),
                    _ => Results.StatusCode(StatusCodes.Status403Forbidden),
                };
            })
            .RequireAuthorization(AuthorizationPolicies.RequireBranchManagerOrAbove)
            .WithName("ReactivateUser")
            .WithTags("Users")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
