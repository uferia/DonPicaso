using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Features.Auth.Login;

namespace Modules.Identity.Features.Auth.Refresh;

public static class RefreshEndpoint
{
    public static IEndpointRouteBuilder MapRefresh(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/refresh", async (
                RefreshCommand command,
                IValidator<RefreshCommand> validator,
                RefreshCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var validation = await validator.ValidateAsync(command, cancellationToken);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary());
                }

                var result = await handler.HandleAsync(command, cancellationToken);
                if (!result.IsSuccess)
                {
                    return Results.Unauthorized();
                }

                return Results.Ok(new LoginResponse(
                    result.AccessToken!, result.AccessTokenExpiresAtUtc!.Value,
                    result.RefreshToken!, result.RefreshTokenExpiresAtUtc!.Value));
            })
            .WithName("RefreshToken")
            .WithTags("Auth")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        return app;
    }
}
