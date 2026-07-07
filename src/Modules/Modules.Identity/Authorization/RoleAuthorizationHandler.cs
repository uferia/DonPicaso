using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Modules.Identity.Features.Users;

namespace Modules.Identity.Authorization;

public sealed class RoleAuthorizationHandler : AuthorizationHandler<RoleRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RoleRequirement requirement)
    {
        var roleClaim = context.User.FindFirst("role")?.Value;

        // Lower numeric rank = higher in the hierarchy (see UserRole's
        // declaration order), so "outranks or matches" is <=.
        if (roleClaim is null ||
            !Enum.TryParse<UserRole>(roleClaim, out var role) ||
            (int)role > (int)requirement.MinimumRole)
        {
            return Task.CompletedTask;
        }

        // Corporate bypasses tenancy-scope checks entirely; every other
        // role that passed the rank check above must also match the
        // resource's brand/branch, when the requirement asks for it.
        if (requirement.Scope != TenancyScope.None && role != UserRole.Corporate && !IsInScope(context, requirement))
        {
            return Task.CompletedTask;
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }

    private static bool IsInScope(AuthorizationHandlerContext context, RoleRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext || requirement.RouteParameterName is null)
        {
            return false;
        }

        var claimName = requirement.Scope == TenancyScope.Brand ? "brandId" : "branchId";
        var claimValue = context.User.FindFirst(claimName)?.Value;
        var routeValue = httpContext.Request.RouteValues[requirement.RouteParameterName]?.ToString();

        return claimValue is not null && routeValue is not null &&
            string.Equals(claimValue, routeValue, StringComparison.OrdinalIgnoreCase);
    }
}
