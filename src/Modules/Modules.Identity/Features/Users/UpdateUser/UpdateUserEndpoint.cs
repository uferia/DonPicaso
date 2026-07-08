using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Users.CreateUser;

namespace Modules.Identity.Features.Users.UpdateUser;

public static class UpdateUserEndpoint
{
    public static IEndpointRouteBuilder MapUpdateUser(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/v1/users/{userId:guid}", async (
                Guid userId,
                UpdateUserCommand command,
                ClaimsPrincipal principal,
                IValidator<UpdateUserCommand> validator,
                UpdateUserCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var validation = await validator.ValidateAsync(command, cancellationToken);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary());
                }

                var requester = RequestingUserContext.FromPrincipal(principal);
                var result = await handler.HandleAsync(requester, userId, command, cancellationToken);

                return result.Error switch
                {
                    UserOperationError.None => Results.Ok(result.User),
                    UserOperationError.NotFound => Results.NotFound(),
                    UserOperationError.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                    _ => Results.StatusCode(StatusCodes.Status400BadRequest),
                };
            })
            .RequireAuthorization(AuthorizationPolicies.RequireBranchManagerOrAbove)
            .WithName("UpdateUser")
            .WithTags("Users")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem();

        return app;
    }
}
