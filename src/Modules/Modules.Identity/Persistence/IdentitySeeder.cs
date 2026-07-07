using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Branches;
using Modules.Identity.Features.Brands;
using Modules.Identity.Features.Users;

namespace Modules.Identity.Persistence;

/// <summary>
/// Seeds one Brand/Branch and one user per role so the login flow (both
/// admin email+password and staff PIN) is exercisable without a
/// provisioning UI, which doesn't exist until the Admin Dashboard
/// sub-project. Dev/test convenience only - not for production use.
/// </summary>
public static class IdentitySeeder
{
    public const string CorporateEmail = "corporate@donpicaso.dev";
    public const string BrandOwnerEmail = "brandowner@donpicaso.dev";
    public const string BranchManagerEmail = "manager@donpicaso.dev";
    public const string SeedPassword = "Password123!";
    public const string StaffPin = "1234";

    public static async Task SeedAsync(IdentityDbContext dbContext, IPasswordHasher<User> passwordHasher, TimeProvider timeProvider)
    {
        if (await dbContext.Brands.AnyAsync())
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        var brand = Brand.Create("Don Picaso Original", now);
        var branch = Branch.Create(brand.Id, "Don Picaso - Downtown", now);

        var corporate = User.CreateAdmin(
            CorporateEmail, HashPassword(passwordHasher, SeedPassword), "Corporate Admin",
            UserRole.Corporate, brandId: null, branchId: null, now);

        var brandOwner = User.CreateAdmin(
            BrandOwnerEmail, HashPassword(passwordHasher, SeedPassword), "Brand Owner",
            UserRole.BrandOwner, brand.Id, branchId: null, now);

        var branchManager = User.CreateAdmin(
            BranchManagerEmail, HashPassword(passwordHasher, SeedPassword), "Branch Manager",
            UserRole.BranchManager, brand.Id, branch.Id, now);

        var staff = User.CreateStaff(
            HashPassword(passwordHasher, StaffPin), "Staff Member",
            brand.Id, branch.Id, now);

        dbContext.Brands.Add(brand);
        dbContext.Branches.Add(branch);
        dbContext.Users.AddRange(corporate, brandOwner, branchManager, staff);

        await dbContext.SaveChangesAsync();
    }

    // PasswordHasher<TUser>.HashPassword doesn't read the user instance
    // (it only exists for generic dispatch), so a throwaway is safe here.
    private static string HashPassword(IPasswordHasher<User> passwordHasher, string plainText) =>
        passwordHasher.HashPassword(null!, plainText);
}
