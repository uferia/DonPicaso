using System.Security.Claims;
using FluentAssertions;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Users;

namespace Modules.Identity.Tests.Authorization;

[TestClass]
public sealed class RequestingUserContextTests
{
    [TestMethod]
    public void FromPrincipal_WithAllClaimsPresent_ParsesEachOne()
    {
        var brandId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var identity = new ClaimsIdentity([
            new Claim("role", UserRole.BranchManager.ToString()),
            new Claim("brandId", brandId.ToString()),
            new Claim("branchId", branchId.ToString()),
        ]);

        var context = RequestingUserContext.FromPrincipal(new ClaimsPrincipal(identity));

        context.Role.Should().Be(UserRole.BranchManager);
        context.BrandId.Should().Be(brandId);
        context.BranchId.Should().Be(branchId);
    }

    [TestMethod]
    public void FromPrincipal_ForACorporateUserWithNoBrandOrBranchClaims_LeavesThemNull()
    {
        var identity = new ClaimsIdentity([new Claim("role", UserRole.Corporate.ToString())]);

        var context = RequestingUserContext.FromPrincipal(new ClaimsPrincipal(identity));

        context.Role.Should().Be(UserRole.Corporate);
        context.BrandId.Should().BeNull();
        context.BranchId.Should().BeNull();
    }
}
