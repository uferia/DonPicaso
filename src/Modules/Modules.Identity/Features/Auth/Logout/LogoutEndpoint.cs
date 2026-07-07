using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Modules.Identity.Features.Auth.Logout;

public static class LogoutEndpoint
{
    public static IEndpointRouteBuilder MapLogout(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/logout", async (
                LogoutCommand command,
                LogoutCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                await handler.HandleAsync(command, cancellationToken);
                return Results.NoContent();
            })
            .WithName("Logout")
            .WithTags("Auth")
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }
}
