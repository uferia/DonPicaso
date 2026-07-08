using FluentAssertions;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Users;

namespace Modules.Identity.Tests.Features.Users;

[TestClass]
public sealed class UserProvisioningRulesTests
{
    [TestMethod]
    public void CanAssign_Corporate_CanAssignAnyRoleAnywhere()
    {
        var requester = new RequestingUserContext(UserRole.Corporate, BrandId: null, BranchId: null);

        UserProvisioningRules.CanAssign(requester, UserRole.BrandOwner, Guid.NewGuid(), null).Should().BeTrue();
        UserProvisioningRules.CanAssign(requester, UserRole.Staff, Guid.NewGuid(), Guid.NewGuid()).Should().BeTrue();
    }

    [TestMethod]
    public void CanAssign_BrandOwner_CanAssignBranchManagerOrStaffWithinOwnBrand()
    {
        var brandId = Guid.NewGuid();
        var requester = new RequestingUserContext(UserRole.BrandOwner, brandId, BranchId: null);

        UserProvisioningRules.CanAssign(requester, UserRole.BranchManager, brandId, Guid.NewGuid()).Should().BeTrue();
        UserProvisioningRules.CanAssign(requester, UserRole.Staff, brandId, Guid.NewGuid()).Should().BeTrue();
    }

    [TestMethod]
    public void CanAssign_BrandOwner_CannotAssignAnotherBrandsUsers()
    {
        var requester = new RequestingUserContext(UserRole.BrandOwner, Guid.NewGuid(), BranchId: null);

        UserProvisioningRules.CanAssign(requester, UserRole.Staff, Guid.NewGuid(), Guid.NewGuid()).Should().BeFalse();
    }

    [TestMethod]
    public void CanAssign_BrandOwner_CannotAssignBrandOwnerOrCorporate()
    {
        var brandId = Guid.NewGuid();
        var requester = new RequestingUserContext(UserRole.BrandOwner, brandId, BranchId: null);

        UserProvisioningRules.CanAssign(requester, UserRole.BrandOwner, brandId, null).Should().BeFalse();
        UserProvisioningRules.CanAssign(requester, UserRole.Corporate, null, null).Should().BeFalse();
    }

    [TestMethod]
    public void CanAssign_BranchManager_CanOnlyAssignStaffWithinOwnBranch()
    {
        var brandId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var requester = new RequestingUserContext(UserRole.BranchManager, brandId, branchId);

        UserProvisioningRules.CanAssign(requester, UserRole.Staff, brandId, branchId).Should().BeTrue();
        UserProvisioningRules.CanAssign(requester, UserRole.Staff, brandId, Guid.NewGuid()).Should().BeFalse();
        UserProvisioningRules.CanAssign(requester, UserRole.BranchManager, brandId, branchId).Should().BeFalse();
    }

    [TestMethod]
    public void CanAssign_Staff_CanNeverAssignAnyone()
    {
        var requester = new RequestingUserContext(UserRole.Staff, Guid.NewGuid(), Guid.NewGuid());

        UserProvisioningRules.CanAssign(requester, UserRole.Staff, requester.BrandId, requester.BranchId).Should().BeFalse();
    }
}
