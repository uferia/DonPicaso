using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Modules.Identity.Features.Auth.StaffRoster;

public static class StaffRosterEndpoint
{
    public static IEndpointRouteBuilder MapStaffRoster(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/auth/staff/{branchId:guid}/users", async (
                Guid branchId,
                GetStaffRosterQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                var roster = await handler.HandleAsync(new GetStaffRosterQuery(branchId), cancellationToken);
                return Results.Ok(roster);
            })
            .WithName("GetStaffRoster")
            .WithTags("Auth")
            .Produces<IReadOnlyList<StaffRosterMember>>(StatusCodes.Status200OK);

        return app;
    }
}
