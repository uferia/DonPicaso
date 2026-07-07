using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Users;

namespace Modules.Identity.Tests.Authorization;

[TestClass]
public sealed class RoleAuthorizationHandlerTests
{
    private readonly RoleAuthorizationHandler _handler = new();

    [TestMethod]
    public async Task HandleAsync_WhenUserRoleOutranksMinimum_Succeeds()
    {
        var context = BuildContext(UserRole.Corporate, new RoleRequirement(UserRole.BranchManager));

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [TestMethod]
    public async Task HandleAsync_WhenUserRoleExactlyMatchesMinimum_Succeeds()
    {
        var context = BuildContext(UserRole.BranchManager, new RoleRequirement(UserRole.BranchManager));

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [TestMethod]
    public async Task HandleAsync_WhenUserRoleIsBelowMinimum_Fails()
    {
        var context = BuildContext(UserRole.Staff, new RoleRequirement(UserRole.BranchManager));

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WhenRoleClaimIsMissing_Fails()
    {
        var context = new AuthorizationHandlerContext(
            [new RoleRequirement(UserRole.Staff)], new ClaimsPrincipal(new ClaimsIdentity()), resource: null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WithNoneScope_BehavesAsRoleRankOnlyWithNoResourceNeeded()
    {
        // Confirms the existing four named policies (Scope = None, the
        // default) are unaffected by the tenancy-scope addition - no
        // HttpContext/resource needed at all, same as before this fix.
        var context = BuildContext(UserRole.BranchManager, new RoleRequirement(UserRole.BranchManager));

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [TestMethod]
    public async Task HandleAsync_WhenCorporateWithScopedRequirement_SucceedsEvenWithoutAMatchingRoute()
    {
        // Corporate bypasses tenancy-scope checks entirely, per the design
        // spec - no route value or resource is needed at all.
        var requirement = new RoleRequirement(UserRole.BranchManager, TenancyScope.Branch, "branchId");
        var context = BuildScopedContext(UserRole.Corporate, requirement, branchClaim: Guid.NewGuid().ToString(), routeBranchId: null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [TestMethod]
    public async Task HandleAsync_WhenNonCorporateRouteBranchMatchesClaim_Succeeds()
    {
        var branchId = Guid.NewGuid().ToString();
        var requirement = new RoleRequirement(UserRole.BranchManager, TenancyScope.Branch, "branchId");
        var context = BuildScopedContext(UserRole.BranchManager, requirement, branchClaim: branchId, routeBranchId: branchId);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [TestMethod]
    public async Task HandleAsync_WhenNonCorporateRouteBranchMismatchesClaim_Fails()
    {
        var requirement = new RoleRequirement(UserRole.BranchManager, TenancyScope.Branch, "branchId");
        var context = BuildScopedContext(
            UserRole.BranchManager, requirement, branchClaim: Guid.NewGuid().ToString(), routeBranchId: Guid.NewGuid().ToString());

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WhenNonCorporateAndRouteHasNoMatchingParameter_Fails()
    {
        // The HttpContext resource is present, but its RouteValues don't
        // contain "branchId" at all (e.g. a route that doesn't declare it).
        var requirement = new RoleRequirement(UserRole.BranchManager, TenancyScope.Branch, "branchId");
        var context = BuildScopedContext(UserRole.BranchManager, requirement, branchClaim: Guid.NewGuid().ToString(), routeBranchId: null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WhenNonCorporateAndResourceIsNotAnHttpContext_Fails()
    {
        // context.Resource is null (as it would be for non-minimal-API
        // authorization, or simply not populated) rather than an HttpContext.
        var identity = new ClaimsIdentity([
            new Claim("role", UserRole.BranchManager.ToString()),
            new Claim("branchId", Guid.NewGuid().ToString()),
        ]);
        var requirement = new RoleRequirement(UserRole.BranchManager, TenancyScope.Branch, "branchId");
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), resource: null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    private static AuthorizationHandlerContext BuildContext(UserRole userRole, RoleRequirement requirement)
    {
        var identity = new ClaimsIdentity([new Claim("role", userRole.ToString())]);
        return new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), resource: null);
    }

    private static AuthorizationHandlerContext BuildScopedContext(
        UserRole userRole, RoleRequirement requirement, string branchClaim, string? routeBranchId)
    {
        var identity = new ClaimsIdentity([
            new Claim("role", userRole.ToString()),
            new Claim("branchId", branchClaim),
        ]);

        var httpContext = new DefaultHttpContext();
        if (routeBranchId is not null)
        {
            httpContext.Request.RouteValues = new RouteValueDictionary { ["branchId"] = routeBranchId };
        }

        return new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), httpContext);
    }
}
