using Microsoft.AspNetCore.Authorization;
using Modules.Identity.Features.Users;

namespace Modules.Identity.Authorization;

public sealed class RoleAuthorizationHandler : AuthorizationHandler<RoleRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RoleRequirement requirement)
    {
        var roleClaim = context.User.FindFirst("role")?.Value;

        // Lower numeric rank = higher in the hierarchy (see UserRole's
        // declaration order), so "outranks or matches" is <=.
        if (roleClaim is not null &&
            Enum.TryParse<UserRole>(roleClaim, out var role) &&
            (int)role <= (int)requirement.MinimumRole)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
