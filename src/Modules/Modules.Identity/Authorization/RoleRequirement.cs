using Microsoft.AspNetCore.Authorization;
using Modules.Identity.Features.Users;

namespace Modules.Identity.Authorization;

/// <summary>
/// The tenancy dimension a <see cref="RoleRequirement"/> additionally scopes
/// access to, beyond role rank. <see cref="UserRole.Corporate"/> callers
/// always bypass this check regardless of scope.
/// </summary>
public enum TenancyScope
{
    None,
    Brand,
    Branch,
}

public sealed class RoleRequirement(
    UserRole minimumRole, TenancyScope scope = TenancyScope.None, string? routeParameterName = null)
    : IAuthorizationRequirement
{
    public UserRole MinimumRole { get; } = minimumRole;

    public TenancyScope Scope { get; } = scope;

    /// <summary>
    /// The route value name (e.g. "branchId") whose value is compared
    /// against the caller's matching claim when <see cref="Scope"/> is not
    /// <see cref="TenancyScope.None"/>.
    /// </summary>
    public string? RouteParameterName { get; } = routeParameterName;
}
