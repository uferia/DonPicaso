using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Users.CreateUser;

namespace Modules.Identity.Features.Users.GetUser;

public static class GetUserEndpoint
{
    public static IEndpointRouteBuilder MapGetUser(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/users/{userId:guid}", async (
                Guid userId, ClaimsPrincipal principal, GetUserQueryHandler handler, CancellationToken cancellationToken) =>
            {
                var requester = RequestingUserContext.FromPrincipal(principal);
                var result = await handler.HandleAsync(requester, userId, cancellationToken);

                return result.Error switch
                {
                    UserOperationError.None => Results.Ok(result.User),
                    UserOperationError.NotFound => Results.NotFound(),
                    _ => Results.StatusCode(StatusCodes.Status403Forbidden),
                };
            })
            .RequireAuthorization(AuthorizationPolicies.RequireBranchManagerOrAbove)
            .WithName("GetUser")
            .WithTags("Users")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
