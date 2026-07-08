using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Users.CreateUser;

namespace Modules.Identity.Features.Users.ResetCredential;

public static class ResetCredentialEndpoint
{
    public static IEndpointRouteBuilder MapResetCredential(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/users/{userId:guid}/reset-credential", async (
                Guid userId,
                ResetCredentialCommand command,
                ClaimsPrincipal principal,
                IValidator<ResetCredentialCommand> validator,
                ResetCredentialCommandHandler handler,
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
            .WithName("ResetCredential")
            .WithTags("Users")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem();

        return app;
    }
}
