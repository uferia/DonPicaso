using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
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

    private static AuthorizationHandlerContext BuildContext(UserRole userRole, RoleRequirement requirement)
    {
        var identity = new ClaimsIdentity([new Claim("role", userRole.ToString())]);
        return new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), resource: null);
    }
}
