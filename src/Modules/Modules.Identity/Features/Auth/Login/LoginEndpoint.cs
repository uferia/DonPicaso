using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Modules.Identity.Features.Auth.Login;

public static class LoginEndpoint
{
    public static IEndpointRouteBuilder MapLogin(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/login", async (
                LoginCommand command,
                IValidator<LoginCommand> validator,
                LoginCommandHandler handler,
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
            .WithName("Login")
            .WithTags("Auth")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        return app;
    }
}

public sealed record LoginResponse(
    string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, string RefreshToken, DateTimeOffset RefreshTokenExpiresAtUtc);
