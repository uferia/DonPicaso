# Identity, Tenancy & Login Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a new `Modules.Identity` backend module (Brand/Branch/User/RefreshToken schema, JWT auth, role+tenancy authorization) and the Angular login/staff-login UI, so the app has a working, testable auth system before Menu and Admin Dashboard are built on top of it.

**Architecture:** A new vertical-slice module (`Modules.Identity`) mirrors `Modules.Sales` exactly — its own `IdentityDbContext` (schema `identity`), FluentValidation validators, minimal-API endpoints per feature, and an `AddIdentityModule`/`MapIdentityModule` composition-root pair wired into `Program.cs`. JWT bearer tokens carry `sub`/`role`/`brandId`/`branchId` claims; a custom `AuthorizationHandler` enforces the 4-tier role hierarchy. Angular gets a `core/auth` module (service, interceptor, guard) plus two login surfaces (`/login` for admins, `/staff-login` + `/device-setup` for the shared POS tablet).

**Tech Stack:** .NET 10 / ASP.NET Core minimal APIs, EF Core + Npgsql (Postgres), FluentValidation, MSTest + FluentAssertions + Moq, Angular 21 standalone components, Vitest.

## Global Constraints

- Target framework for all C# projects: `net10.0`.
- Angular: `21.1.0`, standalone components only (no NgModules), SCSS styling, Vitest as the test runner (via `@angular/build:unit-test` — no separate `vitest.config.ts`; spec files use ambient globals `describe`/`it`/`expect`/`vi` with zero test-framework imports, matching `frontend/src/app/app.spec.ts`).
- `FluentValidation.DependencyInjectionExtensions` `12.1.1`; `Microsoft.EntityFrameworkCore` `10.0.9`; `Npgsql.EntityFrameworkCore.PostgreSQL` `10.0.2`.
- Test packages (backend): `MSTest` `4.0.2`, `FluentAssertions` `6.12.2`, `Microsoft.EntityFrameworkCore.InMemory` `10.0.9`, `Moq` `4.20.72`.
- Postgres connection string (same physical database as `SalesDb`, new `identity` schema): `Host=localhost;Port=5432;Database=restaurant_empire;Username=postgres;Password=postgres`.
- DB naming convention: snake_case tables/columns, explicit `pk_`/`fk_`/`ix_`/`ux_` constraint names (per `OrderEntityConfiguration`).
- Role hierarchy (highest to lowest): `Corporate` > `BrandOwner` > `BranchManager` > `Staff`. `Corporate` bypasses tenancy-scope checks entirely.
- Corporate/BrandOwner/BranchManager authenticate with email+password; Staff authenticates with a 4-digit PIN on a device pre-configured with a `branchId`.
- JWT claims use plain short strings (`sub`, `role`, `brandId`, `branchId`) — `JwtBearerOptions.MapInboundClaims` is explicitly set to `false` so ASP.NET Core doesn't remap them to long WS-* claim URIs on the way in (a default behavior that would otherwise silently break every claim lookup in this plan).
- A dev-only JWT signing key is committed to `appsettings.json`, matching the existing convention of plaintext local Postgres credentials already in that file. Production secrets management is out of scope for this phase.
- No customer-facing login exists or is built in this phase.

## Tasks Overview

1. Scaffold `Modules.Identity` + `Modules.Identity.Tests` projects, empty `IdentityDbContext`, wire into `Program.cs`/solution.
2. `Brand` & `Branch` entities + EF configuration + persistence test.
3. `User` & `RefreshToken` entities + EF configuration + persistence test.
4. JWT + role-authorization infrastructure (`JwtTokenService`, `RoleAuthorizationHandler`, `/me` endpoint) + tests.
5. `Login` feature (email+password) + tests.
6. `StaffLogin` + `StaffRoster` features (PIN login) + tests.
7. `Refresh` feature + tests.
8. `Logout` feature + tests.
9. Initial migration + seed data + manual end-to-end verification.
10. Angular `auth.models.ts` + `AuthService` + tests.
11. Angular `authInterceptor` + tests.
12. Angular `roleGuard` + tests.
13. `Login` page component + tests.
14. `DeviceSetup` + `StaffLogin` page components + tests.
15. Final route/config wiring (`admin`/`pos` guarded placeholders) + manual E2E smoke test.

---

### Task 1: Scaffold `Modules.Identity` module + wire into the host

**Files:**
- Create: `src/Modules/Modules.Identity/Modules.Identity.csproj`
- Create: `src/Modules/Modules.Identity/Persistence/IdentityDbContext.cs`
- Create: `src/Modules/Modules.Identity/IdentityModule.cs`
- Create: `tests/Modules.Identity.Tests/Modules.Identity.Tests.csproj`
- Modify: `src/RestaurantEmpire.Api/RestaurantEmpire.Api.csproj` (add project reference)
- Modify: `src/RestaurantEmpire.Api/Program.cs`
- Modify: `src/RestaurantEmpire.Api/appsettings.json` (add `IdentityDb` connection string)
- Modify: `RestaurantEmpire.sln` (via `dotnet sln add`)

**Interfaces:**
- Produces: `Modules.Identity.IdentityModule.AddIdentityModule(this IServiceCollection services, string connectionString)`, `Modules.Identity.IdentityModule.MapIdentityModule(this IEndpointRouteBuilder app)`, `Modules.Identity.Persistence.IdentityDbContext` (schema `"identity"`, no `DbSet`s yet).

- [ ] **Step 1: Create the `Modules.Identity` project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.9" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.2" />
  </ItemGroup>

</Project>
```

Save this as `src/Modules/Modules.Identity/Modules.Identity.csproj`.

- [ ] **Step 2: Create the empty `IdentityDbContext`**

```csharp
using Microsoft.EntityFrameworkCore;

namespace Modules.Identity.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // Picks up IEntityTypeConfiguration<T> implementations added by later
        // tasks as each entity is introduced (feature-first, like Modules.Sales).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
```

- [ ] **Step 3: Create the `IdentityModule` composition root**

```csharp
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Identity.Persistence;

namespace Modules.Identity;

/// <summary>
/// Composition root for the Identity module. The host (API bootstrap project)
/// calls these two methods; everything else stays internal to the module.
/// </summary>
public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));

        return services;
    }

    public static IEndpointRouteBuilder MapIdentityModule(this IEndpointRouteBuilder app)
    {
        return app;
    }
}
```

- [ ] **Step 4: Create the `Modules.Identity.Tests` project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="FluentAssertions" Version="6.12.2" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.9" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="MSTest" Version="4.0.2" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Microsoft.VisualStudio.TestTools.UnitTesting" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Modules\Modules.Identity\Modules.Identity.csproj" />
  </ItemGroup>

</Project>
```

Save this as `tests/Modules.Identity.Tests/Modules.Identity.Tests.csproj`.

- [ ] **Step 5: Add both new projects to the solution**

Run:
```bash
dotnet sln RestaurantEmpire.sln add src/Modules/Modules.Identity/Modules.Identity.csproj
dotnet sln RestaurantEmpire.sln add tests/Modules.Identity.Tests/Modules.Identity.Tests.csproj
```
Expected: both commands print `Project ... added to the solution.`

- [ ] **Step 6: Reference `Modules.Identity` from the API host**

Modify `src/RestaurantEmpire.Api/RestaurantEmpire.Api.csproj`, adding a second `ProjectReference` next to the existing one:

```xml
  <ItemGroup>
    <ProjectReference Include="..\Modules\Modules.Sales\Modules.Sales.csproj" />
    <ProjectReference Include="..\Modules\Modules.Identity\Modules.Identity.csproj" />
  </ItemGroup>
```

- [ ] **Step 7: Add the `IdentityDb` connection string**

Modify `src/RestaurantEmpire.Api/appsettings.json`, adding a sibling key inside `ConnectionStrings`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "SalesDb": "Host=localhost;Port=5432;Database=restaurant_empire;Username=postgres;Password=postgres",
    "IdentityDb": "Host=localhost;Port=5432;Database=restaurant_empire;Username=postgres;Password=postgres"
  },
  "Cors": {
    "AllowedOrigins": [
      "capacitor://localhost",
      "ionic://localhost",
      "http://localhost",
      "https://localhost",
      "http://localhost:4200"
    ]
  }
}
```

- [ ] **Step 8: Wire `AddIdentityModule`/`MapIdentityModule` into `Program.cs`**

Replace the full contents of `src/RestaurantEmpire.Api/Program.cs` with:

```csharp
using Modules.Identity;
using Modules.Sales;

var builder = WebApplication.CreateBuilder(args);

const string PosCorsPolicy = "PosTablets";

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// The Capacitor WebView serves the Angular bundle from its own origin
// (capacitor://localhost on iOS, http(s)://localhost on Android), so the
// API must explicitly allow those origins.
builder.Services.AddCors(options => options.AddPolicy(PosCorsPolicy, policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddSalesModule(
    builder.Configuration.GetConnectionString("SalesDb")
        ?? throw new InvalidOperationException("Connection string 'SalesDb' is not configured."));

builder.Services.AddIdentityModule(
    builder.Configuration.GetConnectionString("IdentityDb")
        ?? throw new InvalidOperationException("Connection string 'IdentityDb' is not configured."));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(PosCorsPolicy);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapSalesModule();
app.MapIdentityModule();

app.Run();
```

- [ ] **Step 9: Verify the solution builds**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors (warnings, if any, must not be new ones introduced by this task).

- [ ] **Step 10: Commit**

```bash
git add src/Modules/Modules.Identity tests/Modules.Identity.Tests src/RestaurantEmpire.Api RestaurantEmpire.sln
git commit -m "Scaffold Modules.Identity project and wire it into the API host"
```

---

### Task 2: `Brand` & `Branch` entities

**Files:**
- Create: `src/Modules/Modules.Identity/Features/Brands/Brand.cs`
- Create: `src/Modules/Modules.Identity/Features/Brands/BrandEntityConfiguration.cs`
- Create: `src/Modules/Modules.Identity/Features/Branches/Branch.cs`
- Create: `src/Modules/Modules.Identity/Features/Branches/BranchEntityConfiguration.cs`
- Modify: `src/Modules/Modules.Identity/Persistence/IdentityDbContext.cs:1-15` (add two `DbSet`s)
- Test: `tests/Modules.Identity.Tests/Persistence/BrandBranchPersistenceTests.cs`

**Interfaces:**
- Consumes: `IdentityDbContext` from Task 1.
- Produces: `Brand.Create(string name, DateTimeOffset createdAtUtc) -> Brand` (`Id`, `Name`, `CreatedAtUtc`); `Branch.Create(Guid brandId, string name, DateTimeOffset createdAtUtc) -> Branch` (`Id`, `BrandId`, `Name`, `CreatedAtUtc`). Both used by later tasks (User scoping, seed data).

- [ ] **Step 1: Write the failing persistence test**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Branches;
using Modules.Identity.Features.Brands;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Persistence;

[TestClass]
public sealed class BrandBranchPersistenceTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task Branch_WhenSavedUnderABrand_ReloadsWithTheSameBrandId()
    {
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        var branch = Branch.Create(brand.Id, "Downtown", FixedUtcNow);

        _dbContext.Brands.Add(brand);
        _dbContext.Branches.Add(branch);
        await _dbContext.SaveChangesAsync();

        var reloadedBranch = await _dbContext.Branches.SingleAsync(b => b.Id == branch.Id);

        reloadedBranch.BrandId.Should().Be(brand.Id);
        reloadedBranch.Name.Should().Be("Downtown");
    }
}
```

Save as `tests/Modules.Identity.Tests/Persistence/BrandBranchPersistenceTests.cs`.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Modules.Identity.Tests --filter Branch_WhenSavedUnderABrand_ReloadsWithTheSameBrandId`
Expected: FAIL to compile — `Brand`, `Branch`, and `IdentityDbContext.Brands`/`.Branches` don't exist yet.

- [ ] **Step 3: Create the `Brand` entity**

```csharp
namespace Modules.Identity.Features.Brands;

public sealed class Brand
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Brand()
    {
        // EF Core materialization.
    }

    public static Brand Create(string name, DateTimeOffset createdAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAtUtc = createdAtUtc,
        };
}
```

- [ ] **Step 4: Create the `Brand` EF configuration**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Modules.Identity.Features.Brands;

internal sealed class BrandEntityConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("brands");

        builder.HasKey(b => b.Id).HasName("pk_brands");

        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(b => b.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(b => b.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
```

- [ ] **Step 5: Create the `Branch` entity**

```csharp
namespace Modules.Identity.Features.Branches;

public sealed class Branch
{
    public Guid Id { get; private set; }

    public Guid BrandId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Branch()
    {
        // EF Core materialization.
    }

    public static Branch Create(Guid brandId, string name, DateTimeOffset createdAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            BrandId = brandId,
            Name = name,
            CreatedAtUtc = createdAtUtc,
        };
}
```

- [ ] **Step 6: Create the `Branch` EF configuration**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Identity.Features.Brands;

namespace Modules.Identity.Features.Branches;

internal sealed class BranchEntityConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches");

        builder.HasKey(b => b.Id).HasName("pk_branches");

        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(b => b.BrandId).HasColumnName("brand_id").IsRequired();

        builder.Property(b => b.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(b => b.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasOne<Brand>()
            .WithMany()
            .HasForeignKey(b => b.BrandId)
            .HasConstraintName("fk_branches_brand_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.BrandId).HasDatabaseName("ix_branches_brand_id");
    }
}
```

- [ ] **Step 7: Add the two `DbSet`s to `IdentityDbContext`**

Modify `src/Modules/Modules.Identity/Persistence/IdentityDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Branches;
using Modules.Identity.Features.Brands;

namespace Modules.Identity.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<Brand> Brands => Set<Brand>();

    public DbSet<Branch> Branches => Set<Branch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
```

- [ ] **Step 8: Run the test to verify it passes**

Run: `dotnet test tests/Modules.Identity.Tests --filter Branch_WhenSavedUnderABrand_ReloadsWithTheSameBrandId`
Expected: PASS (1 passed).

- [ ] **Step 9: Commit**

```bash
git add src/Modules/Modules.Identity tests/Modules.Identity.Tests
git commit -m "Add Brand and Branch entities to Modules.Identity"
```

---

### Task 3: `User` & `RefreshToken` entities

**Files:**
- Create: `src/Modules/Modules.Identity/Features/Users/UserRole.cs`
- Create: `src/Modules/Modules.Identity/Features/Users/User.cs`
- Create: `src/Modules/Modules.Identity/Features/Users/UserEntityConfiguration.cs`
- Create: `src/Modules/Modules.Identity/Features/Auth/RefreshToken.cs`
- Create: `src/Modules/Modules.Identity/Features/Auth/RefreshTokenEntityConfiguration.cs`
- Modify: `src/Modules/Modules.Identity/Persistence/IdentityDbContext.cs` (add two `DbSet`s)
- Test: `tests/Modules.Identity.Tests/Persistence/UserPersistenceTests.cs`

**Interfaces:**
- Consumes: `Brand`, `Branch` from Task 2.
- Produces: `UserRole` enum (`Corporate`, `BrandOwner`, `BranchManager`, `Staff` — in this order, since the authorization handler in Task 4 compares enum ranks directly). `User.CreateAdmin(string email, string passwordHash, string displayName, UserRole role, Guid? brandId, Guid? branchId, DateTimeOffset createdAtUtc) -> User` — throws `ArgumentException` if `role` is `UserRole.Staff` (Staff accounts must go through `CreateStaff`). `User.CreateStaff(string pinHash, string displayName, Guid brandId, Guid branchId, DateTimeOffset createdAtUtc) -> User` — no `role` parameter; always sets `Role = UserRole.Staff` internally, so role and credential-shape (PIN vs. password) can never mismatch. Properties: `Id`, `Email` (nullable), `PasswordHash` (nullable), `DisplayName`, `PinHash` (nullable), `Role`, `BrandId` (nullable), `BranchId` (nullable), `CreatedAtUtc`. `RefreshToken.Create(Guid userId, string tokenHash, DateTimeOffset expiresAtUtc) -> RefreshToken`; `RefreshToken.Revoke(DateTimeOffset revokedAtUtc)`; properties `Id`, `UserId`, `TokenHash`, `ExpiresAtUtc`, `RevokedAtUtc` (nullable).

- [ ] **Step 1: Write the failing persistence tests**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Auth;
using Modules.Identity.Features.Users;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Persistence;

[TestClass]
public sealed class UserPersistenceTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task StaffUser_WhenSaved_ReloadsWithBrandAndBranchScopeAndNoEmail()
    {
        var brandId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var staff = User.CreateStaff("pin-hash", "Staff Member", brandId, branchId, FixedUtcNow);

        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var reloaded = await _dbContext.Users.SingleAsync(u => u.Id == staff.Id);

        reloaded.Email.Should().BeNull();
        reloaded.PasswordHash.Should().BeNull();
        reloaded.PinHash.Should().Be("pin-hash");
        reloaded.Role.Should().Be(UserRole.Staff);
        reloaded.BrandId.Should().Be(brandId);
        reloaded.BranchId.Should().Be(branchId);
    }

    [TestMethod]
    public async Task RefreshToken_WhenRevoked_PersistsRevokedAtUtc()
    {
        var user = User.CreateAdmin(
            "corporate@donpicaso.dev", "password-hash", "Corporate Admin",
            UserRole.Corporate, brandId: null, branchId: null, FixedUtcNow);
        var token = RefreshToken.Create(user.Id, "token-hash", FixedUtcNow.AddDays(7));

        _dbContext.Users.Add(user);
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        token.Revoke(FixedUtcNow.AddHours(1));
        await _dbContext.SaveChangesAsync();

        var reloaded = await _dbContext.RefreshTokens.SingleAsync(t => t.Id == token.Id);
        reloaded.RevokedAtUtc.Should().Be(FixedUtcNow.AddHours(1));
    }
}
```

Save as `tests/Modules.Identity.Tests/Persistence/UserPersistenceTests.cs`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Modules.Identity.Tests --filter UserPersistenceTests`
Expected: FAIL to compile — `User`, `UserRole`, `RefreshToken`, and the two new `DbSet`s don't exist yet.

- [ ] **Step 3: Create the `UserRole` enum**

```csharp
namespace Modules.Identity.Features.Users;

/// <summary>
/// Ordered highest-to-lowest: the authorization handler (Task 4) compares
/// these numeric ranks directly, so declaration order is load-bearing.
/// </summary>
public enum UserRole
{
    Corporate,
    BrandOwner,
    BranchManager,
    Staff,
}
```

- [ ] **Step 4: Create the `User` entity**

```csharp
namespace Modules.Identity.Features.Users;

public sealed class User
{
    public Guid Id { get; private set; }

    public string? Email { get; private set; }

    public string? PasswordHash { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string? PinHash { get; private set; }

    public UserRole Role { get; private set; }

    public Guid? BrandId { get; private set; }

    public Guid? BranchId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private User()
    {
        // EF Core materialization.
    }

    /// <summary>Corporate, BrandOwner, or BranchManager — logs in with email+password.</summary>
    public static User CreateAdmin(
        string email,
        string passwordHash,
        string displayName,
        UserRole role,
        Guid? brandId,
        Guid? branchId,
        DateTimeOffset createdAtUtc)
    {
        if (role == UserRole.Staff)
        {
            throw new ArgumentException("Staff accounts must be created via CreateStaff.", nameof(role));
        }

        return new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
            DisplayName = displayName,
            Role = role,
            BrandId = brandId,
            BranchId = branchId,
            CreatedAtUtc = createdAtUtc,
        };
    }

    /// <summary>Staff — logs in with a 4-digit PIN on a branch-scoped POS tablet. Role is always Staff; there is no room for a caller to pass a mismatched role.</summary>
    public static User CreateStaff(
        string pinHash,
        string displayName,
        Guid brandId,
        Guid branchId,
        DateTimeOffset createdAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            PinHash = pinHash,
            DisplayName = displayName,
            Role = UserRole.Staff,
            BrandId = brandId,
            BranchId = branchId,
            CreatedAtUtc = createdAtUtc,
        };
}
```

- [ ] **Step 5: Create the `User` EF configuration**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Identity.Features.Branches;
using Modules.Identity.Features.Brands;

namespace Modules.Identity.Features.Users;

internal sealed class UserEntityConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id).HasName("pk_users");

        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(320);

        builder.Property(u => u.PasswordHash).HasColumnName("password_hash");

        builder.Property(u => u.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.PinHash).HasColumnName("pin_hash");

        builder.Property(u => u.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(u => u.BrandId).HasColumnName("brand_id");
        builder.Property(u => u.BranchId).HasColumnName("branch_id");

        builder.Property(u => u.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasOne<Brand>()
            .WithMany()
            .HasForeignKey(u => u.BrandId)
            .HasConstraintName("fk_users_brand_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(u => u.BranchId)
            .HasConstraintName("fk_users_branch_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("email IS NOT NULL")
            .HasDatabaseName("ux_users_email");

        builder.HasIndex(u => u.BrandId).HasDatabaseName("ix_users_brand_id");
        builder.HasIndex(u => u.BranchId).HasDatabaseName("ix_users_branch_id");
    }
}
```

- [ ] **Step 6: Create the `RefreshToken` entity**

```csharp
namespace Modules.Identity.Features.Auth;

public sealed class RefreshToken
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    private RefreshToken()
    {
        // EF Core materialization.
    }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTimeOffset expiresAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
        };

    public void Revoke(DateTimeOffset revokedAtUtc)
    {
        RevokedAtUtc = revokedAtUtc;
    }
}
```

- [ ] **Step 7: Create the `RefreshToken` EF configuration**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Identity.Features.Users;

namespace Modules.Identity.Features.Auth;

internal sealed class RefreshTokenEntityConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(r => r.Id).HasName("pk_refresh_tokens");

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(r => r.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(r => r.RevokedAtUtc)
            .HasColumnName("revoked_at_utc")
            .HasColumnType("timestamptz");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .HasConstraintName("fk_refresh_tokens_user_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.TokenHash).IsUnique().HasDatabaseName("ux_refresh_tokens_token_hash");
        builder.HasIndex(r => r.UserId).HasDatabaseName("ix_refresh_tokens_user_id");
    }
}
```

- [ ] **Step 8: Add the two `DbSet`s to `IdentityDbContext`**

Modify `src/Modules/Modules.Identity/Persistence/IdentityDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Auth;
using Modules.Identity.Features.Branches;
using Modules.Identity.Features.Brands;
using Modules.Identity.Features.Users;

namespace Modules.Identity.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<Brand> Brands => Set<Brand>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test tests/Modules.Identity.Tests --filter UserPersistenceTests`
Expected: PASS (2 passed).

- [ ] **Step 10: Commit**

```bash
git add src/Modules/Modules.Identity tests/Modules.Identity.Tests
git commit -m "Add User and RefreshToken entities to Modules.Identity"
```

---

### Task 4: JWT + role-authorization infrastructure

**Files:**
- Create: `src/Modules/Modules.Identity/Infrastructure/JwtOptions.cs`
- Create: `src/Modules/Modules.Identity/Infrastructure/IJwtTokenService.cs`
- Create: `src/Modules/Modules.Identity/Infrastructure/JwtTokenService.cs`
- Create: `src/Modules/Modules.Identity/Authorization/RoleRequirement.cs`
- Create: `src/Modules/Modules.Identity/Authorization/RoleAuthorizationHandler.cs`
- Create: `src/Modules/Modules.Identity/Authorization/AuthorizationPolicies.cs`
- Create: `src/Modules/Modules.Identity/Features/Auth/Me/MeEndpoint.cs`
- Modify: `src/Modules/Modules.Identity/IdentityModule.cs` (JWT/authorization wiring, new `AddIdentityModule` signature)
- Modify: `src/Modules/Modules.Identity/Modules.Identity.csproj` (add `Microsoft.AspNetCore.Authentication.JwtBearer` — `System.IdentityModel.Tokens.Jwt`/`Microsoft.IdentityModel.Tokens` ship as a separate NuGet package, not part of the `Microsoft.AspNetCore.App` shared framework, so this is required for the code below to compile)
- Modify: `src/RestaurantEmpire.Api/Program.cs` (bind `Jwt` config, call `UseAuthentication`/`UseAuthorization`)
- Modify: `src/RestaurantEmpire.Api/appsettings.json` (add `Jwt` section)
- Test: `tests/Modules.Identity.Tests/Infrastructure/JwtTokenServiceTests.cs`
- Test: `tests/Modules.Identity.Tests/Authorization/RoleAuthorizationHandlerTests.cs`

**Interfaces:**
- Consumes: `User`, `UserRole` from Task 3.
- Produces: `JwtOptions { Issuer, Audience, SigningKey }` (all `required string`). `IJwtTokenService.CreateAccessToken(User user, TimeSpan lifetime) -> AccessToken(string Value, DateTimeOffset ExpiresAtUtc)`; `.GenerateRefreshTokenValue() -> string`; `.HashRefreshToken(string value) -> string`. `RoleRequirement(UserRole minimumRole)`. `AuthorizationPolicies.RequireStaffOrAbove` / `.RequireBranchManagerOrAbove` / `.RequireBrandOwnerOrAbove` / `.RequireCorporate` (policy name constants, used by Tasks 5-9's `[endpoint].RequireAuthorization(...)` calls). `IdentityModule.AddIdentityModule(this IServiceCollection services, string connectionString, JwtOptions jwtOptions)` (signature changes from Task 1 — adds the `jwtOptions` parameter).

**Important implementation note:** JWT claims must use plain short strings (`"sub"`, `"role"`, `"brandId"`, `"branchId"`), and `JwtBearerOptions.MapInboundClaims` must be set to `false`. Without this, ASP.NET Core's default inbound claim mapping silently rewrites well-known claim types (`"sub"` → the long `ClaimTypes.NameIdentifier` URI, etc.) after the token is validated, so `FindFirst("sub")` would return `null` even though the token clearly contains it. Setting `MapInboundClaims = false` keeps the `ClaimsPrincipal` matching exactly what was issued.

- [ ] **Step 1: Write the failing `JwtTokenService` tests**

```csharp
using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;

namespace Modules.Identity.Tests.Infrastructure;

[TestClass]
public sealed class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "DonPicaso.Tests",
        Audience = "DonPicaso.Tests.Pos",
        SigningKey = "unit-test-signing-key-at-least-32-bytes-long!!",
    };

    private readonly JwtTokenService _tokenService = new(Options);

    [TestMethod]
    public void CreateAccessToken_ForBranchScopedUser_IncludesSubRoleBrandAndBranchClaims()
    {
        var user = User.CreateStaff(
            "pin-hash", "Staff Member",
            brandId: Guid.NewGuid(), branchId: Guid.NewGuid(), DateTimeOffset.UtcNow);

        var accessToken = _tokenService.CreateAccessToken(user, TimeSpan.FromMinutes(15));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken.Value);

        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == nameof(UserRole.Staff));
        jwt.Claims.Should().Contain(c => c.Type == "brandId" && c.Value == user.BrandId.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "branchId" && c.Value == user.BranchId.ToString());
    }

    [TestMethod]
    public void CreateAccessToken_ForCorporateUser_OmitsBrandAndBranchClaims()
    {
        var user = User.CreateAdmin(
            "corporate@donpicaso.dev", "password-hash", "Corporate Admin",
            UserRole.Corporate, brandId: null, branchId: null, DateTimeOffset.UtcNow);

        var accessToken = _tokenService.CreateAccessToken(user, TimeSpan.FromMinutes(15));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken.Value);

        jwt.Claims.Should().NotContain(c => c.Type == "brandId");
        jwt.Claims.Should().NotContain(c => c.Type == "branchId");
    }

    [TestMethod]
    public void HashRefreshToken_CalledTwiceWithTheSameValue_ProducesTheSameHash()
    {
        var value = _tokenService.GenerateRefreshTokenValue();

        _tokenService.HashRefreshToken(value).Should().Be(_tokenService.HashRefreshToken(value));
    }

    [TestMethod]
    public void GenerateRefreshTokenValue_CalledTwice_ProducesDifferentValues()
    {
        _tokenService.GenerateRefreshTokenValue().Should().NotBe(_tokenService.GenerateRefreshTokenValue());
    }
}
```

Save as `tests/Modules.Identity.Tests/Infrastructure/JwtTokenServiceTests.cs`.

- [ ] **Step 2: Write the failing `RoleAuthorizationHandler` tests**

```csharp
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
```

Save as `tests/Modules.Identity.Tests/Authorization/RoleAuthorizationHandlerTests.cs`.

- [ ] **Step 3: Run both test files to verify they fail**

Run: `dotnet test tests/Modules.Identity.Tests --filter "JwtTokenServiceTests|RoleAuthorizationHandlerTests"`
Expected: FAIL to compile — none of the referenced types exist yet.

- [ ] **Step 4: Create `JwtOptions`**

```csharp
namespace Modules.Identity.Infrastructure;

public sealed class JwtOptions
{
    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    public required string SigningKey { get; init; }
}
```

- [ ] **Step 5: Create `IJwtTokenService`**

```csharp
using Modules.Identity.Features.Users;

namespace Modules.Identity.Infrastructure;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAtUtc);

public interface IJwtTokenService
{
    AccessToken CreateAccessToken(User user, TimeSpan lifetime);

    string GenerateRefreshTokenValue();

    string HashRefreshToken(string refreshTokenValue);
}
```

- [ ] **Step 6: Create `JwtTokenService`**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Modules.Identity.Features.Users;

namespace Modules.Identity.Infrastructure;

public sealed class JwtTokenService(JwtOptions options) : IJwtTokenService
{
    public AccessToken CreateAccessToken(User user, TimeSpan lifetime)
    {
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(lifetime);

        // Plain short claim types on purpose - see Task 4's implementation
        // note about JwtBearerOptions.MapInboundClaims.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("role", user.Role.ToString()),
        };

        if (user.BrandId is { } brandId)
        {
            claims.Add(new Claim("brandId", brandId.ToString()));
        }

        if (user.BranchId is { } branchId)
        {
            claims.Add(new Claim("branchId", branchId.ToString()));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }

    public string GenerateRefreshTokenValue() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashRefreshToken(string refreshTokenValue) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshTokenValue)));
}
```

- [ ] **Step 7: Create `RoleRequirement`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Modules.Identity.Features.Users;

namespace Modules.Identity.Authorization;

public sealed class RoleRequirement(UserRole minimumRole) : IAuthorizationRequirement
{
    public UserRole MinimumRole { get; } = minimumRole;
}
```

- [ ] **Step 8: Create `RoleAuthorizationHandler`**

```csharp
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
```

- [ ] **Step 9: Create `AuthorizationPolicies`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Modules.Identity.Features.Users;

namespace Modules.Identity.Authorization;

public static class AuthorizationPolicies
{
    public const string RequireStaffOrAbove = nameof(RequireStaffOrAbove);
    public const string RequireBranchManagerOrAbove = nameof(RequireBranchManagerOrAbove);
    public const string RequireBrandOwnerOrAbove = nameof(RequireBrandOwnerOrAbove);
    public const string RequireCorporate = nameof(RequireCorporate);

    public static AuthorizationOptions AddIdentityPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(RequireStaffOrAbove, p => p.Requirements.Add(new RoleRequirement(UserRole.Staff)));
        options.AddPolicy(RequireBranchManagerOrAbove, p => p.Requirements.Add(new RoleRequirement(UserRole.BranchManager)));
        options.AddPolicy(RequireBrandOwnerOrAbove, p => p.Requirements.Add(new RoleRequirement(UserRole.BrandOwner)));
        options.AddPolicy(RequireCorporate, p => p.Requirements.Add(new RoleRequirement(UserRole.Corporate)));
        return options;
    }
}
```

- [ ] **Step 10: Create the `/me` endpoint**

This is the first (and, for this sub-project, only) protected endpoint — it exists so the JWT + authorization stack has a real, working, manually-testable destination (per the spec's Testing section: confirming 401 without a token and 200 with correctly scoped claims), and gives the Angular app a "who am I" call to rehydrate current-user state after a page reload.

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;

namespace Modules.Identity.Features.Auth.Me;

public static class MeEndpoint
{
    public static IEndpointRouteBuilder MapMe(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/auth/me", (ClaimsPrincipal principal) =>
            {
                var userId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
                var role = principal.FindFirstValue("role")!;
                var brandId = principal.FindFirstValue("brandId") is { } b ? Guid.Parse(b) : (Guid?)null;
                var branchId = principal.FindFirstValue("branchId") is { } br ? Guid.Parse(br) : (Guid?)null;

                return Results.Ok(new MeResponse(userId, role, brandId, branchId));
            })
            .RequireAuthorization(AuthorizationPolicies.RequireStaffOrAbove)
            .WithName("GetCurrentUser")
            .WithTags("Auth")
            .Produces<MeResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }
}

public sealed record MeResponse(Guid UserId, string Role, Guid? BrandId, Guid? BranchId);
```

- [ ] **Step 11: Wire JWT auth + authorization + `/me` into `IdentityModule`**

Replace the full contents of `src/Modules/Modules.Identity/IdentityModule.cs`:

```csharp
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Auth.Me;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services, string connectionString, JwtOptions jwtOptions)
    {
        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));

        services.AddValidatorsFromAssembly(typeof(IdentityModule).Assembly, includeInternalTypes: true);

        services.AddSingleton(jwtOptions);
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services.AddSingleton<IAuthorizationHandler, RoleAuthorizationHandler>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // See Task 4's implementation note: without this, ASP.NET
                // Core silently remaps "sub"/"role" to long WS-* claim URIs
                // after validating the token.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                };
            });

        services.AddAuthorization(options => options.AddIdentityPolicies());

        return services;
    }

    public static IEndpointRouteBuilder MapIdentityModule(this IEndpointRouteBuilder app)
    {
        app.MapMe();
        return app;
    }
}
```

- [ ] **Step 12: Add the `Jwt` configuration section**

Modify `src/RestaurantEmpire.Api/appsettings.json`, adding a `Jwt` section as a sibling of `Cors`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "SalesDb": "Host=localhost;Port=5432;Database=restaurant_empire;Username=postgres;Password=postgres",
    "IdentityDb": "Host=localhost;Port=5432;Database=restaurant_empire;Username=postgres;Password=postgres"
  },
  "Cors": {
    "AllowedOrigins": [
      "capacitor://localhost",
      "ionic://localhost",
      "http://localhost",
      "https://localhost",
      "http://localhost:4200"
    ]
  },
  "Jwt": {
    "Issuer": "DonPicaso",
    "Audience": "DonPicaso.Pos",
    "SigningKey": "dev-only-signing-key-do-not-use-in-production-1234567890"
  }
}
```

- [ ] **Step 13: Bind `JwtOptions` and enable auth middleware in `Program.cs`**

Replace the full contents of `src/RestaurantEmpire.Api/Program.cs`:

```csharp
using Modules.Identity;
using Modules.Identity.Infrastructure;
using Modules.Sales;

var builder = WebApplication.CreateBuilder(args);

const string PosCorsPolicy = "PosTablets";

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// The Capacitor WebView serves the Angular bundle from its own origin
// (capacitor://localhost on iOS, http(s)://localhost on Android), so the
// API must explicitly allow those origins.
builder.Services.AddCors(options => options.AddPolicy(PosCorsPolicy, policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddSalesModule(
    builder.Configuration.GetConnectionString("SalesDb")
        ?? throw new InvalidOperationException("Connection string 'SalesDb' is not configured."));

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("'Jwt' configuration section is not configured.");

builder.Services.AddIdentityModule(
    builder.Configuration.GetConnectionString("IdentityDb")
        ?? throw new InvalidOperationException("Connection string 'IdentityDb' is not configured."),
    jwtOptions);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(PosCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapSalesModule();
app.MapIdentityModule();

app.Run();
```

- [ ] **Step 14: Run the tests to verify they pass**

Run: `dotnet test tests/Modules.Identity.Tests --filter "JwtTokenServiceTests|RoleAuthorizationHandlerTests"`
Expected: PASS (8 passed).

- [ ] **Step 15: Verify the solution still builds**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 16: Commit**

```bash
git add src/Modules/Modules.Identity src/RestaurantEmpire.Api tests/Modules.Identity.Tests
git commit -m "Add JWT auth and role-hierarchy authorization to Modules.Identity"
```

---

### Task 5: `Login` feature (email + password)

**Files:**
- Create: `src/Modules/Modules.Identity/Features/Auth/Login/LoginCommand.cs`
- Create: `src/Modules/Modules.Identity/Features/Auth/Login/LoginCommandValidator.cs`
- Create: `src/Modules/Modules.Identity/Features/Auth/Login/LoginCommandHandler.cs`
- Create: `src/Modules/Modules.Identity/Features/Auth/Login/LoginEndpoint.cs`
- Modify: `src/Modules/Modules.Identity/IdentityModule.cs` (register handler, map endpoint)
- Test: `tests/Modules.Identity.Tests/Features/Auth/Login/LoginCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IdentityDbContext`, `User` (Task 3); `IJwtTokenService`, `RefreshToken` (Task 4).
- Produces: `LoginCommand(string Email, string Password)`. `LoginResult(bool IsSuccess, string? AccessToken, DateTimeOffset? AccessTokenExpiresAtUtc, string? RefreshToken, DateTimeOffset? RefreshTokenExpiresAtUtc)` with static factories `LoginResult.Failed()` / `LoginResult.Succeeded(...)` — **reused directly by Tasks 6 and 7** (`StaffLoginCommandHandler`, `RefreshCommandHandler`) instead of each defining their own result type. `LoginResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, string RefreshToken, DateTimeOffset RefreshTokenExpiresAtUtc)` — the wire DTO, also reused by Tasks 6 and 7. `LoginCommandHandler(IdentityDbContext, IValidator<LoginCommand>, IPasswordHasher<User>, IJwtTokenService, TimeProvider).HandleAsync(LoginCommand, CancellationToken) -> Task<LoginResult>`. Route: `POST /api/v1/auth/login`.

- [ ] **Step 1: Write the failing handler tests**

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Modules.Identity.Features.Auth.Login;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Auth.Login;

[TestClass]
public sealed class LoginCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
    private const string Email = "corporate@donpicaso.dev";
    private const string Password = "Password123!";

    private IdentityDbContext _dbContext = null!;
    private PasswordHasher<User> _passwordHasher = null!;
    private LoginCommandHandler _handler = null!;

    [TestInitialize]
    public async Task TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        _passwordHasher = new PasswordHasher<User>();

        var timeProviderMock = new Mock<TimeProvider>();
        timeProviderMock.Setup(t => t.GetUtcNow()).Returns(FixedUtcNow);

        // PasswordHasher<TUser>.HashPassword doesn't read the user instance
        // (it only exists for generic dispatch), so a throwaway is safe here.
        var user = User.CreateAdmin(
            Email, _passwordHasher.HashPassword(null!, Password), "Corporate Admin",
            UserRole.Corporate, brandId: null, branchId: null, FixedUtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _handler = new LoginCommandHandler(
            _dbContext,
            new LoginCommandValidator(),
            _passwordHasher,
            new JwtTokenService(new JwtOptions
            {
                Issuer = "DonPicaso.Tests",
                Audience = "DonPicaso.Tests.Pos",
                SigningKey = "unit-test-signing-key-at-least-32-bytes-long!!",
            }),
            timeProviderMock.Object);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_WithCorrectCredentials_ReturnsTokensAndPersistsARefreshToken()
    {
        var result = await _handler.HandleAsync(new LoginCommand(Email, Password));

        result.IsSuccess.Should().BeTrue();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        (await _dbContext.RefreshTokens.CountAsync()).Should().Be(1);
    }

    [TestMethod]
    public async Task HandleAsync_WithWrongPassword_FailsWithoutPersistingARefreshToken()
    {
        var result = await _handler.HandleAsync(new LoginCommand(Email, "wrong-password"));

        result.IsSuccess.Should().BeFalse();
        (await _dbContext.RefreshTokens.AnyAsync()).Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WithUnknownEmail_FailsTheSameShapeAsAWrongPassword()
    {
        // Same shape either way, so a caller can't use this endpoint to
        // learn whether an email address has an account.
        var knownEmailResult = await _handler.HandleAsync(new LoginCommand(Email, "wrong-password"));
        var unknownEmailResult = await _handler.HandleAsync(new LoginCommand("nobody@donpicaso.dev", "wrong-password"));

        knownEmailResult.IsSuccess.Should().Be(unknownEmailResult.IsSuccess);
        knownEmailResult.AccessToken.Should().Be(unknownEmailResult.AccessToken);
    }
}
```

Save as `tests/Modules.Identity.Tests/Features/Auth/Login/LoginCommandHandlerTests.cs`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Modules.Identity.Tests --filter LoginCommandHandlerTests`
Expected: FAIL to compile — `LoginCommand`, `LoginCommandValidator`, `LoginCommandHandler`, and `IPasswordHasher<User>` registration don't exist yet.

- [ ] **Step 3: Create `LoginCommand`**

```csharp
namespace Modules.Identity.Features.Auth.Login;

public sealed record LoginCommand(string Email, string Password);
```

- [ ] **Step 4: Create `LoginCommandValidator`**

```csharp
using FluentValidation;

namespace Modules.Identity.Features.Auth.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.Password).NotEmpty();
    }
}
```

- [ ] **Step 5: Create `LoginCommandHandler`**

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Auth;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Auth.Login;

public sealed class LoginCommandHandler(
    IdentityDbContext dbContext,
    IValidator<LoginCommand> validator,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService tokenService,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan AdminAccessTokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public async Task<LoginResult> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == command.Email, cancellationToken);

        // Same generic failure whether the email doesn't exist or the
        // password is wrong, so a caller can't use this endpoint to
        // enumerate accounts.
        if (user is null || user.PasswordHash is null ||
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, command.Password) == PasswordVerificationResult.Failed)
        {
            return LoginResult.Failed();
        }

        var now = timeProvider.GetUtcNow();
        var accessToken = tokenService.CreateAccessToken(user, AdminAccessTokenLifetime);
        var refreshTokenValue = tokenService.GenerateRefreshTokenValue();
        var refreshTokenExpiresAt = now.Add(RefreshTokenLifetime);

        dbContext.RefreshTokens.Add(
            RefreshToken.Create(user.Id, tokenService.HashRefreshToken(refreshTokenValue), refreshTokenExpiresAt));
        await dbContext.SaveChangesAsync(cancellationToken);

        return LoginResult.Succeeded(accessToken.Value, accessToken.ExpiresAtUtc, refreshTokenValue, refreshTokenExpiresAt);
    }
}

public sealed record LoginResult(
    bool IsSuccess,
    string? AccessToken,
    DateTimeOffset? AccessTokenExpiresAtUtc,
    string? RefreshToken,
    DateTimeOffset? RefreshTokenExpiresAtUtc)
{
    public static LoginResult Failed() => new(false, null, null, null, null);

    public static LoginResult Succeeded(
        string accessToken, DateTimeOffset accessTokenExpiresAtUtc, string refreshToken, DateTimeOffset refreshTokenExpiresAtUtc) =>
        new(true, accessToken, accessTokenExpiresAtUtc, refreshToken, refreshTokenExpiresAtUtc);
}
```

- [ ] **Step 6: Create `LoginEndpoint`**

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Modules.Identity.Features.Auth.Login;

public static class LoginEndpoint
{
    public static IEndpointRouteBuilder MapLogin(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/login", async (
                LoginCommand command,
                IValidator<LoginCommand> validator,
                LoginCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var validation = await validator.ValidateAsync(command, cancellationToken);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary());
                }

                var result = await handler.HandleAsync(command, cancellationToken);
                if (!result.IsSuccess)
                {
                    return Results.Unauthorized();
                }

                return Results.Ok(new LoginResponse(
                    result.AccessToken!, result.AccessTokenExpiresAtUtc!.Value,
                    result.RefreshToken!, result.RefreshTokenExpiresAtUtc!.Value));
            })
            .WithName("Login")
            .WithTags("Auth")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        return app;
    }
}

public sealed record LoginResponse(
    string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, string RefreshToken, DateTimeOffset RefreshTokenExpiresAtUtc);
```

- [ ] **Step 7: Register the handler and map the endpoint in `IdentityModule`**

Modify `src/Modules/Modules.Identity/IdentityModule.cs`: add `using Modules.Identity.Features.Auth.Login;` to the usings, add `services.AddScoped<LoginCommandHandler>();` and `services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();` (needs `using Microsoft.AspNetCore.Identity;` and `using Modules.Identity.Features.Users;`) right after the `services.AddValidatorsFromAssembly(...)` line in `AddIdentityModule`, and add `app.MapLogin();` as the first line inside `MapIdentityModule` (before `app.MapMe();`).

The two methods should now read:

```csharp
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services, string connectionString, JwtOptions jwtOptions)
    {
        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));

        services.AddValidatorsFromAssembly(typeof(IdentityModule).Assembly, includeInternalTypes: true);

        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<LoginCommandHandler>();

        services.AddSingleton(jwtOptions);
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services.AddSingleton<IAuthorizationHandler, RoleAuthorizationHandler>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                };
            });

        services.AddAuthorization(options => options.AddIdentityPolicies());

        return services;
    }

    public static IEndpointRouteBuilder MapIdentityModule(this IEndpointRouteBuilder app)
    {
        app.MapLogin();
        app.MapMe();
        return app;
    }
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/Modules.Identity.Tests --filter LoginCommandHandlerTests`
Expected: PASS (3 passed).

- [ ] **Step 9: Verify the solution still builds**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 10: Commit**

```bash
git add src/Modules/Modules.Identity tests/Modules.Identity.Tests
git commit -m "Add the admin email+password Login feature"
```

---

### Task 6: `StaffLogin` + `StaffRoster` features (PIN login)

**Files:**
- Create: `src/Modules/Modules.Identity/Features/Auth/StaffLogin/StaffLoginCommand.cs`
- Create: `src/Modules/Modules.Identity/Features/Auth/StaffLogin/StaffLoginCommandValidator.cs`
- Create: `src/Modules/Modules.Identity/Features/Auth/StaffLogin/StaffLoginCommandHandler.cs`
- Create: `src/Modules/Modules.Identity/Features/Auth/StaffLogin/StaffLoginEndpoint.cs`
- Create: `src/Modules/Modules.Identity/Features/Auth/StaffRoster/GetStaffRosterQuery.cs`
- Create: `src/Modules/Modules.Identity/Features/Auth/StaffRoster/GetStaffRosterQueryHandler.cs`
- Create: `src/Modules/Modules.Identity/Features/Auth/StaffRoster/StaffRosterEndpoint.cs`
- Modify: `src/Modules/Modules.Identity/IdentityModule.cs` (register handlers, map endpoints)
- Test: `tests/Modules.Identity.Tests/Features/Auth/StaffLogin/StaffLoginCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `LoginResult`, `LoginResponse` from Task 5 (reused, not redefined). `IdentityDbContext`, `User`, `RefreshToken` from Tasks 3-4.
- Produces: `StaffLoginCommand(Guid BranchId, Guid UserId, string Pin)`. `StaffLoginCommandHandler.HandleAsync(StaffLoginCommand, CancellationToken) -> Task<LoginResult>`. `GetStaffRosterQuery(Guid BranchId)`, `StaffRosterMember(Guid UserId, string DisplayName)`, `GetStaffRosterQueryHandler.HandleAsync(GetStaffRosterQuery, CancellationToken) -> Task<IReadOnlyList<StaffRosterMember>>`. Routes: `POST /api/v1/auth/staff-login`, `GET /api/v1/auth/staff/{branchId}/users`.

- [ ] **Step 1: Write the failing handler tests**

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Modules.Identity.Features.Auth.StaffLogin;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Auth.StaffLogin;

[TestClass]
public sealed class StaffLoginCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
    private const string Pin = "1234";

    private IdentityDbContext _dbContext = null!;
    private Guid _branchId;
    private Guid _staffUserId;
    private StaffLoginCommandHandler _handler = null!;

    [TestInitialize]
    public async Task TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        var passwordHasher = new PasswordHasher<User>();
        _branchId = Guid.NewGuid();

        var staff = User.CreateStaff(
            passwordHasher.HashPassword(null!, Pin), "Staff Member",
            brandId: Guid.NewGuid(), branchId: _branchId, FixedUtcNow);
        _staffUserId = staff.Id;
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var timeProviderMock = new Mock<TimeProvider>();
        timeProviderMock.Setup(t => t.GetUtcNow()).Returns(FixedUtcNow);

        _handler = new StaffLoginCommandHandler(
            _dbContext,
            new StaffLoginCommandValidator(),
            passwordHasher,
            new JwtTokenService(new JwtOptions
            {
                Issuer = "DonPicaso.Tests",
                Audience = "DonPicaso.Tests.Pos",
                SigningKey = "unit-test-signing-key-at-least-32-bytes-long!!",
            }),
            timeProviderMock.Object);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_WithCorrectPin_ReturnsTokens()
    {
        var result = await _handler.HandleAsync(new StaffLoginCommand(_branchId, _staffUserId, Pin));

        result.IsSuccess.Should().BeTrue();
        result.AccessToken.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task HandleAsync_WithWrongPin_Fails()
    {
        var result = await _handler.HandleAsync(new StaffLoginCommand(_branchId, _staffUserId, "9999"));

        result.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WhenUserBelongsToADifferentBranch_Fails()
    {
        var result = await _handler.HandleAsync(new StaffLoginCommand(Guid.NewGuid(), _staffUserId, Pin));

        result.IsSuccess.Should().BeFalse();
    }
}
```

Save as `tests/Modules.Identity.Tests/Features/Auth/StaffLogin/StaffLoginCommandHandlerTests.cs`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Modules.Identity.Tests --filter StaffLoginCommandHandlerTests`
Expected: FAIL to compile — `StaffLoginCommand`/`StaffLoginCommandValidator`/`StaffLoginCommandHandler` don't exist yet.

- [ ] **Step 3: Create `StaffLoginCommand`**

```csharp
namespace Modules.Identity.Features.Auth.StaffLogin;

public sealed record StaffLoginCommand(Guid BranchId, Guid UserId, string Pin);
```

- [ ] **Step 4: Create `StaffLoginCommandValidator`**

```csharp
using FluentValidation;

namespace Modules.Identity.Features.Auth.StaffLogin;

public sealed class StaffLoginCommandValidator : AbstractValidator<StaffLoginCommand>
{
    public StaffLoginCommandValidator()
    {
        RuleFor(c => c.BranchId).NotEmpty();
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Pin).NotEmpty().Length(4);
    }
}
```

- [ ] **Step 5: Create `StaffLoginCommandHandler`**

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Auth.Login;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Auth.StaffLogin;

public sealed class StaffLoginCommandHandler(
    IdentityDbContext dbContext,
    IValidator<StaffLoginCommand> validator,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService tokenService,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan StaffAccessTokenLifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public async Task<LoginResult> HandleAsync(StaffLoginCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var user = await dbContext.Users.FirstOrDefaultAsync(
            u => u.Id == command.UserId && u.BranchId == command.BranchId, cancellationToken);

        if (user is null || user.PinHash is null ||
            passwordHasher.VerifyHashedPassword(user, user.PinHash, command.Pin) == PasswordVerificationResult.Failed)
        {
            return LoginResult.Failed();
        }

        var now = timeProvider.GetUtcNow();
        var accessToken = tokenService.CreateAccessToken(user, StaffAccessTokenLifetime);
        var refreshTokenValue = tokenService.GenerateRefreshTokenValue();
        var refreshTokenExpiresAt = now.Add(RefreshTokenLifetime);

        dbContext.RefreshTokens.Add(
            RefreshToken.Create(user.Id, tokenService.HashRefreshToken(refreshTokenValue), refreshTokenExpiresAt));
        await dbContext.SaveChangesAsync(cancellationToken);

        return LoginResult.Succeeded(accessToken.Value, accessToken.ExpiresAtUtc, refreshTokenValue, refreshTokenExpiresAt);
    }
}
```

- [ ] **Step 6: Create `StaffLoginEndpoint`**

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Features.Auth.Login;

namespace Modules.Identity.Features.Auth.StaffLogin;

public static class StaffLoginEndpoint
{
    public static IEndpointRouteBuilder MapStaffLogin(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/staff-login", async (
                StaffLoginCommand command,
                IValidator<StaffLoginCommand> validator,
                StaffLoginCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var validation = await validator.ValidateAsync(command, cancellationToken);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary());
                }

                var result = await handler.HandleAsync(command, cancellationToken);
                if (!result.IsSuccess)
                {
                    return Results.Unauthorized();
                }

                return Results.Ok(new LoginResponse(
                    result.AccessToken!, result.AccessTokenExpiresAtUtc!.Value,
                    result.RefreshToken!, result.RefreshTokenExpiresAtUtc!.Value));
            })
            .WithName("StaffLogin")
            .WithTags("Auth")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        return app;
    }
}
```

- [ ] **Step 7: Create `GetStaffRosterQuery` and its handler**

```csharp
namespace Modules.Identity.Features.Auth.StaffRoster;

public sealed record GetStaffRosterQuery(Guid BranchId);

public sealed record StaffRosterMember(Guid UserId, string DisplayName);
```

```csharp
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Auth.StaffRoster;

public sealed class GetStaffRosterQueryHandler(IdentityDbContext dbContext)
{
    public async Task<IReadOnlyList<StaffRosterMember>> HandleAsync(
        GetStaffRosterQuery query, CancellationToken cancellationToken = default) =>
        await dbContext.Users
            .Where(u => u.BranchId == query.BranchId && u.PinHash != null)
            .OrderBy(u => u.DisplayName)
            .Select(u => new StaffRosterMember(u.Id, u.DisplayName))
            .ToListAsync(cancellationToken);
}
```

Save the record types as `src/Modules/Modules.Identity/Features/Auth/StaffRoster/GetStaffRosterQuery.cs` and the handler as `src/Modules/Modules.Identity/Features/Auth/StaffRoster/GetStaffRosterQueryHandler.cs`.

- [ ] **Step 8: Create `StaffRosterEndpoint`**

```csharp
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
```

- [ ] **Step 9: Register the handlers and map the endpoints in `IdentityModule`**

Modify `src/Modules/Modules.Identity/IdentityModule.cs`: add `using Modules.Identity.Features.Auth.StaffLogin;` and `using Modules.Identity.Features.Auth.StaffRoster;`, add `services.AddScoped<StaffLoginCommandHandler>();` and `services.AddScoped<GetStaffRosterQueryHandler>();` right after `services.AddScoped<LoginCommandHandler>();`, and add `app.MapStaffLogin();` and `app.MapStaffRoster();` right after `app.MapLogin();` inside `MapIdentityModule`:

```csharp
    public static IEndpointRouteBuilder MapIdentityModule(this IEndpointRouteBuilder app)
    {
        app.MapLogin();
        app.MapStaffLogin();
        app.MapStaffRoster();
        app.MapMe();
        return app;
    }
```

- [ ] **Step 10: Run the tests to verify they pass**

Run: `dotnet test tests/Modules.Identity.Tests --filter StaffLoginCommandHandlerTests`
Expected: PASS (3 passed).

- [ ] **Step 11: Verify the solution still builds**

Run: `dotnet build`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 12: Commit**

```bash
git add src/Modules/Modules.Identity tests/Modules.Identity.Tests
git commit -m "Add the staff PIN login and staff roster features"
```

---

### Task 7: `Refresh` feature

**Files:**
- Create: `src/Modules/Modules.Identity/Features/Auth/Refresh/RefreshCommand.cs`
- Create: `src/Modules/Modules.Identity/Features/Auth/Refresh/RefreshCommandValidator.cs`
- Create: `src/Modules/Modules.Identity/Features/Auth/Refresh/RefreshCommandHandler.cs`
- Create: `src/Modules/Modules.Identity/Features/Auth/Refresh/RefreshEndpoint.cs`
- Modify: `src/Modules/Modules.Identity/IdentityModule.cs` (register handler, map endpoint)
- Test: `tests/Modules.Identity.Tests/Features/Auth/Refresh/RefreshCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `LoginResult`, `LoginResponse` from Task 5. `IdentityDbContext`, `User`, `UserRole`, `RefreshToken` from Tasks 3-4.
- Produces: `RefreshCommand(string RefreshToken)`. `RefreshCommandHandler.HandleAsync(RefreshCommand, CancellationToken) -> Task<LoginResult>`. Route: `POST /api/v1/auth/refresh`. Note: this implementation does **not** rotate the refresh token on use (the same value is returned back and stays valid until its original expiry, or until revoked by logout) — a deliberate simplification not specified either way by the design spec.

- [ ] **Step 1: Write the failing handler tests**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Modules.Identity.Features.Auth;
using Modules.Identity.Features.Auth.Refresh;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Auth.Refresh;

[TestClass]
public sealed class RefreshCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private Mock<TimeProvider> _timeProviderMock = null!;
    private RefreshCommandHandler _handler = null!;
    private string _refreshTokenValue = null!;

    [TestInitialize]
    public async Task TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        var tokenService = new JwtTokenService(new JwtOptions
        {
            Issuer = "DonPicaso.Tests",
            Audience = "DonPicaso.Tests.Pos",
            SigningKey = "unit-test-signing-key-at-least-32-bytes-long!!",
        });

        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(FixedUtcNow);

        var user = User.CreateAdmin(
            "corporate@donpicaso.dev", "password-hash", "Corporate Admin",
            UserRole.Corporate, brandId: null, branchId: null, FixedUtcNow);
        _dbContext.Users.Add(user);

        _refreshTokenValue = tokenService.GenerateRefreshTokenValue();
        _dbContext.RefreshTokens.Add(RefreshToken.Create(
            user.Id, tokenService.HashRefreshToken(_refreshTokenValue), FixedUtcNow.AddDays(7)));
        await _dbContext.SaveChangesAsync();

        _handler = new RefreshCommandHandler(_dbContext, new RefreshCommandValidator(), tokenService, _timeProviderMock.Object);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_WithAnUnexpiredKnownToken_ReturnsANewAccessToken()
    {
        var result = await _handler.HandleAsync(new RefreshCommand(_refreshTokenValue));

        result.IsSuccess.Should().BeTrue();
        result.AccessToken.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task HandleAsync_WithAnUnknownToken_Fails()
    {
        var result = await _handler.HandleAsync(new RefreshCommand("not-a-real-token"));

        result.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WithAnExpiredToken_Fails()
    {
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(FixedUtcNow.AddDays(8));

        var result = await _handler.HandleAsync(new RefreshCommand(_refreshTokenValue));

        result.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WithARevokedToken_Fails()
    {
        var token = await _dbContext.RefreshTokens.SingleAsync();
        token.Revoke(FixedUtcNow);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new RefreshCommand(_refreshTokenValue));

        result.IsSuccess.Should().BeFalse();
    }
}
```

Save as `tests/Modules.Identity.Tests/Features/Auth/Refresh/RefreshCommandHandlerTests.cs`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Modules.Identity.Tests --filter RefreshCommandHandlerTests`
Expected: FAIL to compile — `RefreshCommand`/`RefreshCommandValidator`/`RefreshCommandHandler` don't exist yet.

- [ ] **Step 3: Create `RefreshCommand`**

```csharp
namespace Modules.Identity.Features.Auth.Refresh;

public sealed record RefreshCommand(string RefreshToken);
```

- [ ] **Step 4: Create `RefreshCommandValidator`**

```csharp
using FluentValidation;

namespace Modules.Identity.Features.Auth.Refresh;

public sealed class RefreshCommandValidator : AbstractValidator<RefreshCommand>
{
    public RefreshCommandValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty();
    }
}
```

- [ ] **Step 5: Create `RefreshCommandHandler`**

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Auth.Login;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Auth.Refresh;

public sealed class RefreshCommandHandler(
    IdentityDbContext dbContext,
    IValidator<RefreshCommand> validator,
    IJwtTokenService tokenService,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan AdminAccessTokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan StaffAccessTokenLifetime = TimeSpan.FromHours(12);

    public async Task<LoginResult> HandleAsync(RefreshCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var tokenHash = tokenService.HashRefreshToken(command.RefreshToken);
        var now = timeProvider.GetUtcNow();

        var existing = await dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existing is null || existing.RevokedAtUtc is not null || existing.ExpiresAtUtc <= now)
        {
            return LoginResult.Failed();
        }

        var user = await dbContext.Users.FirstAsync(u => u.Id == existing.UserId, cancellationToken);

        var lifetime = user.Role == UserRole.Staff ? StaffAccessTokenLifetime : AdminAccessTokenLifetime;
        var accessToken = tokenService.CreateAccessToken(user, lifetime);

        return LoginResult.Succeeded(accessToken.Value, accessToken.ExpiresAtUtc, command.RefreshToken, existing.ExpiresAtUtc);
    }
}
```

- [ ] **Step 6: Create `RefreshEndpoint`**

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Features.Auth.Login;

namespace Modules.Identity.Features.Auth.Refresh;

public static class RefreshEndpoint
{
    public static IEndpointRouteBuilder MapRefresh(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/refresh", async (
                RefreshCommand command,
                IValidator<RefreshCommand> validator,
                RefreshCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var validation = await validator.ValidateAsync(command, cancellationToken);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary());
                }

                var result = await handler.HandleAsync(command, cancellationToken);
                if (!result.IsSuccess)
                {
                    return Results.Unauthorized();
                }

                return Results.Ok(new LoginResponse(
                    result.AccessToken!, result.AccessTokenExpiresAtUtc!.Value,
                    result.RefreshToken!, result.RefreshTokenExpiresAtUtc!.Value));
            })
            .WithName("RefreshToken")
            .WithTags("Auth")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        return app;
    }
}
```

- [ ] **Step 7: Register the handler and map the endpoint**

Modify `src/Modules/Modules.Identity/IdentityModule.cs`: add `using Modules.Identity.Features.Auth.Refresh;`, add `services.AddScoped<RefreshCommandHandler>();` after `services.AddScoped<GetStaffRosterQueryHandler>();`, and add `app.MapRefresh();` after `app.MapStaffRoster();` inside `MapIdentityModule`.

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/Modules.Identity.Tests --filter RefreshCommandHandlerTests`
Expected: PASS (4 passed).

- [ ] **Step 9: Commit**

```bash
git add src/Modules/Modules.Identity tests/Modules.Identity.Tests
git commit -m "Add the refresh-token feature"
```

---

### Task 8: `Logout` feature

**Files:**
- Create: `src/Modules/Modules.Identity/Features/Auth/Logout/LogoutCommand.cs`
- Create: `src/Modules/Modules.Identity/Features/Auth/Logout/LogoutCommandHandler.cs`
- Create: `src/Modules/Modules.Identity/Features/Auth/Logout/LogoutEndpoint.cs`
- Modify: `src/Modules/Modules.Identity/IdentityModule.cs` (register handler, map endpoint)
- Test: `tests/Modules.Identity.Tests/Features/Auth/Logout/LogoutCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IdentityDbContext`, `RefreshToken` from Tasks 3-4; `IJwtTokenService` from Task 4.
- Produces: `LogoutCommand(string RefreshToken)`. `LogoutCommandHandler.HandleAsync(LogoutCommand, CancellationToken) -> Task`. Route: `POST /api/v1/auth/logout`.

- [ ] **Step 1: Write the failing handler tests**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Modules.Identity.Features.Auth;
using Modules.Identity.Features.Auth.Logout;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Auth.Logout;

[TestClass]
public sealed class LogoutCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private LogoutCommandHandler _handler = null!;
    private string _refreshTokenValue = null!;

    [TestInitialize]
    public async Task TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        var tokenService = new JwtTokenService(new JwtOptions
        {
            Issuer = "DonPicaso.Tests",
            Audience = "DonPicaso.Tests.Pos",
            SigningKey = "unit-test-signing-key-at-least-32-bytes-long!!",
        });

        var user = User.CreateAdmin(
            "corporate@donpicaso.dev", "password-hash", "Corporate Admin",
            UserRole.Corporate, brandId: null, branchId: null, FixedUtcNow);
        _dbContext.Users.Add(user);

        _refreshTokenValue = tokenService.GenerateRefreshTokenValue();
        _dbContext.RefreshTokens.Add(RefreshToken.Create(
            user.Id, tokenService.HashRefreshToken(_refreshTokenValue), FixedUtcNow.AddDays(7)));
        await _dbContext.SaveChangesAsync();

        var timeProviderMock = new Mock<TimeProvider>();
        timeProviderMock.Setup(t => t.GetUtcNow()).Returns(FixedUtcNow);

        _handler = new LogoutCommandHandler(_dbContext, tokenService, timeProviderMock.Object);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_WithAKnownToken_RevokesIt()
    {
        await _handler.HandleAsync(new LogoutCommand(_refreshTokenValue));

        var token = await _dbContext.RefreshTokens.SingleAsync();
        token.RevokedAtUtc.Should().Be(FixedUtcNow);
    }

    [TestMethod]
    public async Task HandleAsync_WithAnUnknownToken_DoesNotThrow()
    {
        var act = () => _handler.HandleAsync(new LogoutCommand("not-a-real-token"));

        await act.Should().NotThrowAsync();
    }
}
```

Save as `tests/Modules.Identity.Tests/Features/Auth/Logout/LogoutCommandHandlerTests.cs`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Modules.Identity.Tests --filter LogoutCommandHandlerTests`
Expected: FAIL to compile — `LogoutCommand`/`LogoutCommandHandler` don't exist yet.

- [ ] **Step 3: Create `LogoutCommand`**

```csharp
namespace Modules.Identity.Features.Auth.Logout;

public sealed record LogoutCommand(string RefreshToken);
```

- [ ] **Step 4: Create `LogoutCommandHandler`**

```csharp
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Auth.Logout;

public sealed class LogoutCommandHandler(
    IdentityDbContext dbContext,
    IJwtTokenService tokenService,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(LogoutCommand command, CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashRefreshToken(command.RefreshToken);

        var existing = await dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
        if (existing is null || existing.RevokedAtUtc is not null)
        {
            return;
        }

        existing.Revoke(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Create `LogoutEndpoint`**

```csharp
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
```

- [ ] **Step 6: Register the handler and map the endpoint**

Modify `src/Modules/Modules.Identity/IdentityModule.cs`: add `using Modules.Identity.Features.Auth.Logout;`, add `services.AddScoped<LogoutCommandHandler>();` after `services.AddScoped<RefreshCommandHandler>();`, and add `app.MapLogout();` after `app.MapRefresh();` inside `MapIdentityModule`. `MapIdentityModule` should now read:

```csharp
    public static IEndpointRouteBuilder MapIdentityModule(this IEndpointRouteBuilder app)
    {
        app.MapLogin();
        app.MapStaffLogin();
        app.MapStaffRoster();
        app.MapRefresh();
        app.MapLogout();
        app.MapMe();
        return app;
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/Modules.Identity.Tests --filter LogoutCommandHandlerTests`
Expected: PASS (2 passed).

- [ ] **Step 8: Run the full backend test suite**

Run: `dotnet test`
Expected: all tests pass across `Modules.Sales.Tests` and `Modules.Identity.Tests` (0 failed).

- [ ] **Step 9: Commit**

```bash
git add src/Modules/Modules.Identity tests/Modules.Identity.Tests
git commit -m "Add the logout feature"
```

---

### Task 9: Migration + seed data + manual verification

**Files:**
- Create: `src/Modules/Modules.Identity/Persistence/IdentitySeeder.cs`
- Modify: `src/RestaurantEmpire.Api/Program.cs` (invoke the seeder in Development)
- Create (generated): `src/Modules/Modules.Identity/Persistence/Migrations/*InitialIdentitySchema*.cs`

**Interfaces:**
- Consumes: `IdentityDbContext`, `Brand`, `Branch`, `User`, `UserRole` from Tasks 2-3; `IPasswordHasher<User>` from Task 5.
- Produces: `IdentitySeeder.SeedAsync(IdentityDbContext, IPasswordHasher<User>, TimeProvider) -> Task` (idempotent — no-ops if a `Brand` already exists), plus public constants `IdentitySeeder.CorporateEmail`, `.BrandOwnerEmail`, `.BranchManagerEmail`, `.SeedPassword`, `.StaffPin` for use in the manual E2E verification below and in later sub-projects' seed-data-dependent tests.

- [ ] **Step 1: Create `IdentitySeeder`**

```csharp
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
```

- [ ] **Step 2: Invoke the seeder from `Program.cs` in Development**

Modify `src/RestaurantEmpire.Api/Program.cs`: add `using Microsoft.AspNetCore.Identity;`, `using Modules.Identity.Features.Users;`, and `using Modules.Identity.Persistence;` to the usings, and change the `if (app.Environment.IsDevelopment())` block to:

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var seedScope = app.Services.CreateScope();
    await IdentitySeeder.SeedAsync(
        seedScope.ServiceProvider.GetRequiredService<IdentityDbContext>(),
        seedScope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(),
        TimeProvider.System);
}
```

(Top-level statements support `await` directly; no other change to `Program.cs`'s shape is needed.)

- [ ] **Step 3: Generate the initial migration**

Run:
```bash
dotnet tool restore
dotnet ef migrations add InitialIdentitySchema --project src/Modules/Modules.Identity --startup-project src/RestaurantEmpire.Api
```
Expected: a new `Persistence/Migrations/<timestamp>_InitialIdentitySchema.cs` (+ `.Designer.cs`, + updated `IdentityDbContextModelSnapshot.cs`) appears under `src/Modules/Modules.Identity/`, creating the `identity` schema with `brands`, `branches`, `users`, `refresh_tokens` tables matching the entity configurations from Tasks 2-4.

- [ ] **Step 4: Apply the migration against local Postgres**

Run:
```bash
docker compose up -d
dotnet ef database update --project src/Modules/Modules.Identity --startup-project src/RestaurantEmpire.Api
```
Expected: `Done.` with no errors; the `identity` schema and its four tables now exist in the `restaurant_empire` database.

- [ ] **Step 5: Manual end-to-end verification**

Run: `dotnet run --project src/RestaurantEmpire.Api`
Expected: API listens on `http://localhost:5098`; the seeder runs once and inserts the seed Brand/Branch/Users (confirm via `docker exec` + `psql`, or a REST client, that `identity.users` has 4 rows).

With the API running, verify each login path and the `/me` guard:

```bash
curl -s -X POST http://localhost:5098/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"corporate@donpicaso.dev","password":"Password123!"}'
```
Expected: `200 OK` with a JSON body containing `accessToken`, `refreshToken`.

```bash
curl -s http://localhost:5098/api/v1/auth/me
```
Expected: `401 Unauthorized` (no token attached).

```bash
curl -s http://localhost:5098/api/v1/auth/me -H "Authorization: Bearer <accessToken from above>"
```
Expected: `200 OK` with `{"userId":"...","role":"Corporate","brandId":null,"branchId":null}`.

Repeat the login call for `brandowner@donpicaso.dev` and `manager@donpicaso.dev` (same password) and confirm `/me` reflects `BrandOwner`/`BranchManager` with non-null `brandId`/`branchId` as appropriate. Then fetch the staff roster and log in as staff:

```bash
curl -s http://localhost:5098/api/v1/auth/staff/<branchId from a manager's /me response>/users
```
Expected: `200 OK` with one entry, `"displayName":"Staff Member"`.

```bash
curl -s -X POST http://localhost:5098/api/v1/auth/staff-login \
  -H "Content-Type: application/json" \
  -d '{"branchId":"<branchId>","userId":"<userId from roster>","pin":"1234"}'
```
Expected: `200 OK` with tokens; `/me` with that token shows `"role":"Staff"`.

- [ ] **Step 6: Commit**

```bash
git add src/Modules/Modules.Identity src/RestaurantEmpire.Api
git commit -m "Add the initial Identity migration and dev seed data"
```

---

### Task 10: Angular auth models + `AuthService`

**Files:**
- Create: `frontend/src/app/core/auth/auth.models.ts`
- Create: `frontend/src/app/core/auth/auth.service.ts`
- Create: `frontend/src/app/core/auth/auth.service.spec.ts`

**Interfaces:**
- Consumes: backend routes from Tasks 5-8 (`POST /api/v1/auth/login`, `/staff-login`, `/refresh`, `/logout`), response shape from `LoginResponse` (Task 5): `{ accessToken, accessTokenExpiresAtUtc, refreshToken, refreshTokenExpiresAtUtc }`.
- Produces: `Role` enum (`Corporate`, `BrandOwner`, `BranchManager`, `Staff`) — **used by Task 12's `roleGuard`**. `CurrentUser { userId, role, brandId, branchId }`. `AuthService.login(LoginRequest) -> Promise<void>`, `.staffLogin(StaffLoginRequest) -> Promise<void>`, `.logout() -> Promise<void>`, `.refresh() -> Promise<boolean>`, `.getAccessToken() -> string | null`, `.currentUser: Signal<CurrentUser | null>` — **all consumed directly by Task 11's interceptor and Task 12's guard**.

- [ ] **Step 1: Create `auth.models.ts`**

```typescript
export enum Role {
  Corporate = 'Corporate',
  BrandOwner = 'BrandOwner',
  BranchManager = 'BranchManager',
  Staff = 'Staff',
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface StaffLoginRequest {
  branchId: string;
  userId: string;
  pin: string;
}

export interface AuthTokens {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
}

export interface StaffRosterMember {
  userId: string;
  displayName: string;
}
```

Save as `frontend/src/app/core/auth/auth.models.ts`.

- [ ] **Step 2: Write the failing `AuthService` tests**

```typescript
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { AuthService } from './auth.service';

function buildFakeAccessToken(claims: Record<string, string>): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const payload = btoa(JSON.stringify(claims));
  return `${header}.${payload}.fake-signature`;
}

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('decodes the access token into currentUser after a successful login', async () => {
    const loginPromise = service.login({ email: 'corporate@donpicaso.dev', password: 'Password123!' });

    const req = httpMock.expectOne('/api/v1/auth/login');
    req.flush({
      accessToken: buildFakeAccessToken({ sub: 'user-1', role: 'Corporate' }),
      accessTokenExpiresAtUtc: new Date().toISOString(),
      refreshToken: 'refresh-token-value',
      refreshTokenExpiresAtUtc: new Date().toISOString(),
    });
    await loginPromise;

    expect(service.currentUser()).toEqual({
      userId: 'user-1',
      role: 'Corporate',
      brandId: null,
      branchId: null,
    });
    expect(localStorage.getItem('donpicaso.refreshToken')).toBe('refresh-token-value');
  });

  it('clears the session when logout is called', async () => {
    const loginPromise = service.login({ email: 'corporate@donpicaso.dev', password: 'Password123!' });
    httpMock.expectOne('/api/v1/auth/login').flush({
      accessToken: buildFakeAccessToken({ sub: 'user-1', role: 'Corporate' }),
      accessTokenExpiresAtUtc: new Date().toISOString(),
      refreshToken: 'refresh-token-value',
      refreshTokenExpiresAtUtc: new Date().toISOString(),
    });
    await loginPromise;

    const logoutPromise = service.logout();
    httpMock.expectOne('/api/v1/auth/logout').flush({});
    await logoutPromise;

    expect(service.currentUser()).toBeNull();
    expect(service.getAccessToken()).toBeNull();
    expect(localStorage.getItem('donpicaso.refreshToken')).toBeNull();
  });

  it('clears the session when refresh fails', async () => {
    localStorage.setItem('donpicaso.refreshToken', 'stale-token');

    const refreshPromise = service.refresh();
    httpMock
      .expectOne('/api/v1/auth/refresh')
      .flush({ message: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    const result = await refreshPromise;

    expect(result).toBe(false);
    expect(service.currentUser()).toBeNull();
    expect(localStorage.getItem('donpicaso.refreshToken')).toBeNull();
  });

  it('returns false immediately from refresh when there is no stored refresh token', async () => {
    const result = await service.refresh();

    expect(result).toBe(false);
  });
});
```

Save as `frontend/src/app/core/auth/auth.service.spec.ts`.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `npm test` (in `frontend/`)
Expected: FAIL — `./auth.service` does not exist yet.

- [ ] **Step 4: Create `AuthService`**

```typescript
import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { AuthTokens, LoginRequest, Role, StaffLoginRequest } from './auth.models';

export interface CurrentUser {
  userId: string;
  role: Role;
  brandId: string | null;
  branchId: string | null;
}

const LOGIN_URL = '/api/v1/auth/login';
const STAFF_LOGIN_URL = '/api/v1/auth/staff-login';
const REFRESH_URL = '/api/v1/auth/refresh';
const LOGOUT_URL = '/api/v1/auth/logout';
const REFRESH_TOKEN_STORAGE_KEY = 'donpicaso.refreshToken';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private accessToken: string | null = null;

  readonly currentUser = signal<CurrentUser | null>(null);

  async login(request: LoginRequest): Promise<void> {
    const tokens = await firstValueFrom(this.http.post<AuthTokens>(LOGIN_URL, request));
    this.applyTokens(tokens);
  }

  async staffLogin(request: StaffLoginRequest): Promise<void> {
    const tokens = await firstValueFrom(this.http.post<AuthTokens>(STAFF_LOGIN_URL, request));
    this.applyTokens(tokens);
  }

  /**
   * Exchanges the stored refresh token for a new access token. Used on app
   * start (no access token yet after a reload) and by the auth interceptor
   * after a 401.
   */
  async refresh(): Promise<boolean> {
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_STORAGE_KEY);
    if (!refreshToken) {
      return false;
    }

    try {
      const tokens = await firstValueFrom(this.http.post<AuthTokens>(REFRESH_URL, { refreshToken }));
      this.applyTokens(tokens);
      return true;
    } catch {
      this.clearSession();
      return false;
    }
  }

  async logout(): Promise<void> {
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_STORAGE_KEY);
    if (refreshToken) {
      // Best-effort server-side revoke. The local session must clear
      // either way - a shared POS tablet should never appear logged in
      // after the user asked to log out, even if the network dropped or
      // the token was already invalid.
      try {
        await firstValueFrom(this.http.post(LOGOUT_URL, { refreshToken }));
      } catch {
        // Ignored - clearSession() below still runs.
      }
    }
    this.clearSession();
  }

  getAccessToken(): string | null {
    return this.accessToken;
  }

  private applyTokens(tokens: AuthTokens): void {
    this.accessToken = tokens.accessToken;
    localStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, tokens.refreshToken);
    this.currentUser.set(decodeCurrentUser(tokens.accessToken));
  }

  private clearSession(): void {
    this.accessToken = null;
    localStorage.removeItem(REFRESH_TOKEN_STORAGE_KEY);
    this.currentUser.set(null);
  }
}

/**
 * Decodes the JWT payload to read claims client-side. Never used for
 * security decisions (the backend re-validates on every request) - only
 * to populate UI state like the current user's role.
 */
function decodeCurrentUser(accessToken: string): CurrentUser {
  const payloadSegment = accessToken.split('.')[1];
  const base64 = payloadSegment.replace(/-/g, '+').replace(/_/g, '/');
  const payload = JSON.parse(atob(base64)) as Record<string, string>;

  return {
    userId: payload['sub'],
    role: payload['role'] as Role,
    brandId: payload['brandId'] ?? null,
    branchId: payload['branchId'] ?? null,
  };
}
```

Save as `frontend/src/app/core/auth/auth.service.ts`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `npm test` (in `frontend/`)
Expected: PASS (4 passed) for `AuthService`.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/app/core/auth
git commit -m "Add Angular AuthService and auth models"
```

---

### Task 11: `authInterceptor`

**Files:**
- Create: `frontend/src/app/core/auth/auth.interceptor.ts`
- Create: `frontend/src/app/core/auth/auth.interceptor.spec.ts`

**Interfaces:**
- Consumes: `AuthService.getAccessToken()`, `.refresh()` from Task 10.
- Produces: `authInterceptor: HttpInterceptorFn` — **wired into `app.config.ts` in Task 15**.

- [ ] **Step 1: Write the failing interceptor tests**

```typescript
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let authService: AuthService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
  });

  afterEach(() => httpMock.verify());

  it('attaches the bearer token when one is present', async () => {
    vi.spyOn(authService, 'getAccessToken').mockReturnValue('access-token-value');

    const responsePromise = firstValueFrom(http.get('/api/v1/some-resource'));
    const req = httpMock.expectOne('/api/v1/some-resource');

    expect(req.request.headers.get('Authorization')).toBe('Bearer access-token-value');
    req.flush({});
    await responsePromise;
  });

  it('retries the request with a new token after a silent refresh succeeds on a 401', async () => {
    vi.spyOn(authService, 'getAccessToken')
      .mockReturnValueOnce('expired-token')
      .mockReturnValueOnce('fresh-token');
    vi.spyOn(authService, 'refresh').mockResolvedValue(true);

    const responsePromise = firstValueFrom(http.get('/api/v1/some-resource'));

    const firstAttempt = httpMock.expectOne('/api/v1/some-resource');
    firstAttempt.flush({ message: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    // The retry-after-refresh path resolves over a couple of microtasks
    // (the refresh() promise, then the switchMap); yield the event loop so
    // the retried request has been dispatched before we look for it.
    await Promise.resolve();
    await Promise.resolve();
    await Promise.resolve();

    const retryAttempt = httpMock.expectOne('/api/v1/some-resource');
    expect(retryAttempt.request.headers.get('Authorization')).toBe('Bearer fresh-token');
    retryAttempt.flush({ ok: true });

    await responsePromise;
  });

  it('redirects to /login and gives up when the silent refresh also fails', async () => {
    vi.spyOn(authService, 'getAccessToken').mockReturnValue('expired-token');
    vi.spyOn(authService, 'refresh').mockResolvedValue(false);
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    const responsePromise = firstValueFrom(http.get('/api/v1/some-resource'));
    const req = httpMock.expectOne('/api/v1/some-resource');
    req.flush({ message: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    await expect(responsePromise).rejects.toBeTruthy();
    expect(navigateSpy).toHaveBeenCalledWith('/login');
  });
});
```

Save as `frontend/src/app/core/auth/auth.interceptor.spec.ts`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `npm test` (in `frontend/`)
Expected: FAIL — `./auth.interceptor` does not exist yet.

- [ ] **Step 3: Create `authInterceptor`**

```typescript
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';

import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const accessToken = authService.getAccessToken();
  const authorizedReq = accessToken
    ? req.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })
    : req;

  return next(authorizedReq).pipe(
    catchError((error: unknown) => {
      // Auth endpoints failing with 401 (bad credentials/expired refresh
      // token) must not trigger another refresh attempt - that would loop.
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || req.url.includes('/api/v1/auth/')) {
        return throwError(() => error);
      }

      return from(authService.refresh()).pipe(
        switchMap((refreshed) => {
          if (!refreshed) {
            void router.navigateByUrl('/login');
            return throwError(() => error);
          }

          const retriedToken = authService.getAccessToken();
          const retriedReq = req.clone({ setHeaders: { Authorization: `Bearer ${retriedToken}` } });
          return next(retriedReq);
        }),
      );
    }),
  );
};
```

Save as `frontend/src/app/core/auth/auth.interceptor.ts`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `npm test` (in `frontend/`)
Expected: PASS (3 passed) for `authInterceptor`.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/core/auth
git commit -m "Add Angular auth HTTP interceptor"
```

---

### Task 12: `roleGuard`

**Files:**
- Create: `frontend/src/app/core/auth/auth.guard.ts`
- Create: `frontend/src/app/core/auth/auth.guard.spec.ts`

**Interfaces:**
- Consumes: `AuthService.currentUser` (Task 10), `Role` (Task 10).
- Produces: `roleGuard(minimumRole: Role) -> CanActivateFn` — **used by Task 15's route config for `/admin` and `/pos`**.

- [ ] **Step 1: Write the failing guard tests**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { Role } from './auth.models';
import { roleGuard } from './auth.guard';

describe('roleGuard', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])],
    });
  });

  it('allows access when the current user outranks the minimum role', () => {
    const authService = TestBed.inject(AuthService);
    authService.currentUser.set({ userId: 'u1', role: Role.Corporate, brandId: null, branchId: null });

    const result = TestBed.runInInjectionContext(() => roleGuard(Role.BranchManager)({} as never, {} as never));

    expect(result).toBe(true);
  });

  it('redirects to /login when there is no current user', () => {
    const router = TestBed.inject(Router);

    const result = TestBed.runInInjectionContext(() => roleGuard(Role.Staff)({} as never, {} as never));

    expect(result).toEqual(router.parseUrl('/login'));
  });

  it('redirects to /login when the current user is below the minimum role', () => {
    const authService = TestBed.inject(AuthService);
    authService.currentUser.set({ userId: 'u1', role: Role.Staff, brandId: null, branchId: null });
    const router = TestBed.inject(Router);

    const result = TestBed.runInInjectionContext(() => roleGuard(Role.BranchManager)({} as never, {} as never));

    expect(result).toEqual(router.parseUrl('/login'));
  });
});
```

Save as `frontend/src/app/core/auth/auth.guard.spec.ts`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `npm test` (in `frontend/`)
Expected: FAIL — `./auth.guard` does not exist yet.

- [ ] **Step 3: Create `roleGuard`**

```typescript
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { Role } from './auth.models';
import { AuthService } from './auth.service';

const ROLE_RANK: Record<Role, number> = {
  [Role.Corporate]: 0,
  [Role.BrandOwner]: 1,
  [Role.BranchManager]: 2,
  [Role.Staff]: 3,
};

export function roleGuard(minimumRole: Role): CanActivateFn {
  return () => {
    const authService = inject(AuthService);
    const router = inject(Router);

    const user = authService.currentUser();
    if (user && ROLE_RANK[user.role] <= ROLE_RANK[minimumRole]) {
      return true;
    }

    return router.parseUrl('/login');
  };
}
```

Save as `frontend/src/app/core/auth/auth.guard.ts`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `npm test` (in `frontend/`)
Expected: PASS (3 passed) for `roleGuard`.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/core/auth
git commit -m "Add Angular role-based route guard"
```

---

### Task 13: `Login` page component

**Files:**
- Create: `frontend/src/app/features/auth/login/login.ts`
- Create: `frontend/src/app/features/auth/login/login.html`
- Create: `frontend/src/app/features/auth/login/login.scss`
- Create: `frontend/src/app/features/auth/login/login.spec.ts`

**Interfaces:**
- Consumes: `AuthService.login()` (Task 10).
- Produces: `Login` standalone component (selector `app-login`) — **routed at `/login` in Task 15**.

- [ ] **Step 1: Write the failing component tests**

```typescript
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { Login } from './login';

function buildFakeAccessToken(claims: Record<string, string>): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const payload = btoa(JSON.stringify(claims));
  return `${header}.${payload}.fake-signature`;
}

describe('Login', () => {
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => httpMock.verify());

  it('navigates to /admin after a successful login', async () => {
    const fixture = TestBed.createComponent(Login);
    const component = fixture.componentInstance;
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    component['email'] = 'corporate@donpicaso.dev';
    component['password'] = 'Password123!';
    const submitPromise = component.submit();

    const req = httpMock.expectOne('/api/v1/auth/login');
    req.flush({
      accessToken: buildFakeAccessToken({ sub: 'user-1', role: 'Corporate' }),
      accessTokenExpiresAtUtc: new Date().toISOString(),
      refreshToken: 'refresh-token-value',
      refreshTokenExpiresAtUtc: new Date().toISOString(),
    });
    await submitPromise;

    expect(navigateSpy).toHaveBeenCalledWith('/admin');
  });

  it('shows an error message when the credentials are rejected', async () => {
    const fixture = TestBed.createComponent(Login);
    const component = fixture.componentInstance;

    const submitPromise = component.submit();
    const req = httpMock.expectOne('/api/v1/auth/login');
    req.flush({ message: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    await submitPromise;

    expect(component['errorMessage']()).toBe('Invalid email or password.');
  });
});
```

Save as `frontend/src/app/features/auth/login/login.spec.ts`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `npm test` (in `frontend/`)
Expected: FAIL — `./login` does not exist yet.

- [ ] **Step 3: Create `login.html`**

```html
<form class="login-form" (ngSubmit)="submit()">
  <h1>Sign in</h1>

  <label>
    Email
    <input type="email" name="email" [(ngModel)]="email" required autocomplete="username" />
  </label>

  <label>
    Password
    <input type="password" name="password" [(ngModel)]="password" required autocomplete="current-password" />
  </label>

  @if (errorMessage()) {
    <p class="error">{{ errorMessage() }}</p>
  }

  <button type="submit" [disabled]="isSubmitting()">Sign in</button>
</form>
```

Save as `frontend/src/app/features/auth/login/login.html`.

- [ ] **Step 4: Create `login.scss`**

```scss
.login-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  max-width: 320px;
  margin: 4rem auto;

  label {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }

  .error {
    color: #b00020;
  }
}
```

Save as `frontend/src/app/features/auth/login/login.scss`.

- [ ] **Step 5: Create `login.ts`**

```typescript
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected email = '';
  protected password = '';
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isSubmitting = signal(false);

  async submit(): Promise<void> {
    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    try {
      await this.authService.login({ email: this.email, password: this.password });
      await this.router.navigateByUrl('/admin');
    } catch {
      this.errorMessage.set('Invalid email or password.');
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
```

Save as `frontend/src/app/features/auth/login/login.ts`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `npm test` (in `frontend/`)
Expected: PASS (2 passed) for `Login`.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/app/features/auth/login
git commit -m "Add the admin Login page"
```

---

### Task 14: `DeviceSetup` + `StaffLogin` page components

**Files:**
- Create: `frontend/src/app/features/auth/device-setup/device-setup.ts`
- Create: `frontend/src/app/features/auth/device-setup/device-setup.html`
- Create: `frontend/src/app/features/auth/device-setup/device-setup.scss`
- Create: `frontend/src/app/features/auth/device-setup/device-setup.spec.ts`
- Create: `frontend/src/app/features/auth/staff-login/staff-login.ts`
- Create: `frontend/src/app/features/auth/staff-login/staff-login.html`
- Create: `frontend/src/app/features/auth/staff-login/staff-login.scss`
- Create: `frontend/src/app/features/auth/staff-login/staff-login.spec.ts`

**Interfaces:**
- Consumes: `AuthService.staffLogin()`, `StaffRosterMember` (Task 10); `GET /api/v1/auth/staff/{branchId}/users` (Task 6).
- Produces: `DEVICE_BRANCH_ID_STORAGE_KEY` constant + `DeviceSetup` component — **imported by `StaffLogin` below**. `StaffLogin` standalone component (selector `app-staff-login`) — **routed at `/staff-login` and `/device-setup` in Task 15**.

- [ ] **Step 1: Write the failing `DeviceSetup` tests**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { DEVICE_BRANCH_ID_STORAGE_KEY, DeviceSetup } from './device-setup';

describe('DeviceSetup', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [DeviceSetup],
      providers: [provideRouter([])],
    });
  });

  it('stores the branch id and navigates to /staff-login on save', () => {
    const fixture = TestBed.createComponent(DeviceSetup);
    const component = fixture.componentInstance;
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    component['branchId'] = 'branch-123';
    component.save();

    expect(localStorage.getItem(DEVICE_BRANCH_ID_STORAGE_KEY)).toBe('branch-123');
    expect(navigateSpy).toHaveBeenCalledWith('/staff-login');
  });

  it('shows an error and does not navigate when branch id is blank', () => {
    const fixture = TestBed.createComponent(DeviceSetup);
    const component = fixture.componentInstance;
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    component.save();

    expect(component['errorMessage']()).toBe('Branch ID is required.');
    expect(navigateSpy).not.toHaveBeenCalled();
  });
});
```

Save as `frontend/src/app/features/auth/device-setup/device-setup.spec.ts`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `npm test` (in `frontend/`)
Expected: FAIL — `./device-setup` does not exist yet.

- [ ] **Step 3: Create `device-setup.html` and `device-setup.scss`**

```html
<form class="device-setup-form" (ngSubmit)="save()">
  <h1>Set up this device</h1>
  <p>Enter the branch ID this tablet belongs to. You only need to do this once.</p>

  <label>
    Branch ID
    <input type="text" name="branchId" [(ngModel)]="branchId" required />
  </label>

  @if (errorMessage()) {
    <p class="error">{{ errorMessage() }}</p>
  }

  <button type="submit">Save</button>
</form>
```

Save as `frontend/src/app/features/auth/device-setup/device-setup.html`.

```scss
.device-setup-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  max-width: 320px;
  margin: 4rem auto;

  label {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }

  .error {
    color: #b00020;
  }
}
```

Save as `frontend/src/app/features/auth/device-setup/device-setup.scss`.

- [ ] **Step 4: Create `device-setup.ts`**

```typescript
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

export const DEVICE_BRANCH_ID_STORAGE_KEY = 'donpicaso.deviceBranchId';

@Component({
  selector: 'app-device-setup',
  imports: [FormsModule],
  templateUrl: './device-setup.html',
  styleUrl: './device-setup.scss',
})
export class DeviceSetup {
  private readonly router = inject(Router);

  protected branchId = '';
  protected readonly errorMessage = signal<string | null>(null);

  save(): void {
    if (!this.branchId.trim()) {
      this.errorMessage.set('Branch ID is required.');
      return;
    }

    localStorage.setItem(DEVICE_BRANCH_ID_STORAGE_KEY, this.branchId.trim());
    void this.router.navigateByUrl('/staff-login');
  }
}
```

Save as `frontend/src/app/features/auth/device-setup/device-setup.ts`.

- [ ] **Step 5: Run the `DeviceSetup` tests to verify they pass**

Run: `npm test` (in `frontend/`)
Expected: PASS (2 passed) for `DeviceSetup`.

- [ ] **Step 6: Write the failing `StaffLogin` tests**

```typescript
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { DEVICE_BRANCH_ID_STORAGE_KEY } from '../device-setup/device-setup';
import { StaffLogin } from './staff-login';

describe('StaffLogin', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [StaffLogin],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('redirects to /device-setup when no branch id is configured', async () => {
    const fixture = TestBed.createComponent(StaffLogin);
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    await fixture.componentInstance.ngOnInit();

    expect(navigateSpy).toHaveBeenCalledWith('/device-setup');
  });

  it('loads the staff roster for the configured branch', async () => {
    localStorage.setItem(DEVICE_BRANCH_ID_STORAGE_KEY, 'branch-123');

    const fixture = TestBed.createComponent(StaffLogin);
    const initPromise = fixture.componentInstance.ngOnInit();

    const req = httpMock.expectOne('/api/v1/auth/staff/branch-123/users');
    req.flush([{ userId: 'user-1', displayName: 'Ana' }]);
    await initPromise;

    expect(fixture.componentInstance['roster']()).toEqual([{ userId: 'user-1', displayName: 'Ana' }]);
  });

  it('logs in and navigates to /pos after a correct 4-digit pin', async () => {
    localStorage.setItem(DEVICE_BRANCH_ID_STORAGE_KEY, 'branch-123');
    const fixture = TestBed.createComponent(StaffLogin);
    const component = fixture.componentInstance;
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');
    const authService = TestBed.inject(AuthService);
    vi.spyOn(authService, 'staffLogin').mockResolvedValue(undefined);

    const initPromise = component.ngOnInit();
    httpMock.expectOne('/api/v1/auth/staff/branch-123/users').flush([{ userId: 'user-1', displayName: 'Ana' }]);
    await initPromise;

    component.selectMember({ userId: 'user-1', displayName: 'Ana' });
    '1234'.split('').forEach((digit) => component.pressDigit(digit));
    await component.submitPin();

    expect(authService.staffLogin).toHaveBeenCalledWith({ branchId: 'branch-123', userId: 'user-1', pin: '1234' });
    expect(navigateSpy).toHaveBeenCalledWith('/pos');
  });
});
```

Save as `frontend/src/app/features/auth/staff-login/staff-login.spec.ts`.

- [ ] **Step 7: Run the tests to verify they fail**

Run: `npm test` (in `frontend/`)
Expected: FAIL — `./staff-login` does not exist yet.

- [ ] **Step 8: Create `staff-login.html` and `staff-login.scss`**

```html
@if (!selectedMember()) {
  <div class="roster">
    <h1>Who's working?</h1>
    @for (member of roster(); track member.userId) {
      <button type="button" class="roster-member" (click)="selectMember(member)">
        {{ member.displayName }}
      </button>
    }
  </div>
} @else {
  <div class="pin-pad">
    <h1>{{ selectedMember()!.displayName }}</h1>
    <p class="pin-dots">{{ pin().padEnd(4, '•').split('').join(' ') }}</p>

    @if (errorMessage()) {
      <p class="error">{{ errorMessage() }}</p>
    }

    <div class="digits">
      @for (digit of digits; track digit) {
        <button type="button" (click)="pressDigit(digit)">{{ digit }}</button>
      }
    </div>

    <button type="button" [disabled]="pin().length < 4" (click)="submitPin()">Enter</button>
  </div>
}
```

Save as `frontend/src/app/features/auth/staff-login/staff-login.html`.

```scss
.roster {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
  margin: 4rem auto;

  .roster-member {
    padding: 1rem 2rem;
    font-size: 1.25rem;
  }
}

.pin-pad {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
  margin: 4rem auto;

  .pin-dots {
    font-size: 2rem;
    letter-spacing: 0.5rem;
  }

  .digits {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 0.5rem;

    button {
      padding: 1rem;
      font-size: 1.5rem;
    }
  }

  .error {
    color: #b00020;
  }
}
```

Save as `frontend/src/app/features/auth/staff-login/staff-login.scss`.

- [ ] **Step 9: Create `staff-login.ts`**

```typescript
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { StaffRosterMember } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';
import { DEVICE_BRANCH_ID_STORAGE_KEY } from '../device-setup/device-setup';

@Component({
  selector: 'app-staff-login',
  templateUrl: './staff-login.html',
  styleUrl: './staff-login.scss',
})
export class StaffLogin implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly digits = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '0'];
  protected readonly roster = signal<StaffRosterMember[]>([]);
  protected readonly selectedMember = signal<StaffRosterMember | null>(null);
  protected readonly pin = signal('');
  protected readonly errorMessage = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    const branchId = localStorage.getItem(DEVICE_BRANCH_ID_STORAGE_KEY);
    if (!branchId) {
      // Fire-and-forget, matching DeviceSetup.save() below: on the
      // installed Angular version (21.2.17), navigateByUrl() to a route
      // absent from a test's empty provideRouter([]) table rejects its
      // promise rather than resolving false, which would otherwise
      // propagate out of ngOnInit() and fail the caller.
      void this.router.navigateByUrl('/device-setup');
      return;
    }

    this.roster.set(
      await firstValueFrom(this.http.get<StaffRosterMember[]>(`/api/v1/auth/staff/${branchId}/users`)),
    );
  }

  selectMember(member: StaffRosterMember): void {
    this.selectedMember.set(member);
    this.pin.set('');
    this.errorMessage.set(null);
  }

  pressDigit(digit: string): void {
    if (this.pin().length < 4) {
      this.pin.set(this.pin() + digit);
    }
  }

  async submitPin(): Promise<void> {
    const branchId = localStorage.getItem(DEVICE_BRANCH_ID_STORAGE_KEY);
    const member = this.selectedMember();
    if (!branchId || !member) {
      return;
    }

    try {
      await this.authService.staffLogin({ branchId, userId: member.userId, pin: this.pin() });
    } catch {
      this.errorMessage.set('Incorrect PIN.');
      this.pin.set('');
      return;
    }

    // Login succeeded - a navigation failure here isn't a PIN error, so it's
    // handled separately (fire-and-forget, matching DeviceSetup.save() and
    // StaffLogin.ngOnInit()'s redirect) rather than reusing the "Incorrect
    // PIN." message.
    void this.router.navigateByUrl('/pos');
  }
}
```

Save as `frontend/src/app/features/auth/staff-login/staff-login.ts`.

- [ ] **Step 10: Run the tests to verify they pass**

Run: `npm test` (in `frontend/`)
Expected: PASS (3 passed) for `StaffLogin`.

- [ ] **Step 11: Commit**

```bash
git add frontend/src/app/features/auth/device-setup frontend/src/app/features/auth/staff-login
git commit -m "Add the device-setup and staff PIN login pages"
```

---

### Task 15: Final route/config wiring + manual E2E smoke test

**Files:**
- Create: `frontend/src/app/features/admin/admin-placeholder.ts`
- Create: `frontend/src/app/features/pos/pos-placeholder.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/app.config.ts`

**Interfaces:**
- Consumes: `roleGuard` (Task 12), `Role` (Task 10), `authInterceptor` (Task 11), `Login`/`DeviceSetup`/`StaffLogin` components (Tasks 13-14).
- Produces: fully wired routes `/login`, `/device-setup`, `/staff-login`, `/admin` (guarded, `BranchManager`+), `/pos` (guarded, `Staff`+). `AdminPlaceholder`/`PosPlaceholder` are intentionally minimal — the real Admin Dashboard and Menu/POS UIs are later sub-projects; these exist only so the guarded routes have somewhere to land, proving the guard chain end-to-end.

- [ ] **Step 1: Create the placeholder destination components**

```typescript
import { Component } from '@angular/core';

@Component({
  selector: 'app-admin-placeholder',
  template: `<p>Admin dashboard coming soon.</p>`,
})
export class AdminPlaceholder {}
```

Save as `frontend/src/app/features/admin/admin-placeholder.ts`.

```typescript
import { Component } from '@angular/core';

@Component({
  selector: 'app-pos-placeholder',
  template: `<p>POS menu coming soon.</p>`,
})
export class PosPlaceholder {}
```

Save as `frontend/src/app/features/pos/pos-placeholder.ts`.

- [ ] **Step 2: Wire the routes**

Replace the full contents of `frontend/src/app/app.routes.ts`:

```typescript
import { Routes } from '@angular/router';

import { roleGuard } from './core/auth/auth.guard';
import { Role } from './core/auth/auth.models';
import { DeviceSetup } from './features/auth/device-setup/device-setup';
import { Login } from './features/auth/login/login';
import { StaffLogin } from './features/auth/staff-login/staff-login';

export const routes: Routes = [
  { path: 'login', component: Login },
  { path: 'device-setup', component: DeviceSetup },
  { path: 'staff-login', component: StaffLogin },
  {
    path: 'admin',
    canActivate: [roleGuard(Role.BranchManager)],
    loadComponent: () => import('./features/admin/admin-placeholder').then((m) => m.AdminPlaceholder),
  },
  {
    path: 'pos',
    canActivate: [roleGuard(Role.Staff)],
    loadComponent: () => import('./features/pos/pos-placeholder').then((m) => m.PosPlaceholder),
  },
];
```

- [ ] **Step 3: Wire the auth interceptor into the HTTP client**

Replace the full contents of `frontend/src/app/app.config.ts`:

```typescript
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { authInterceptor } from './core/auth/auth.interceptor';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
  ],
};
```

- [ ] **Step 4: Run the full frontend test suite**

Run: `npm test` (in `frontend/`)
Expected: all specs pass (0 failed) — `App`, `AuthService`, `authInterceptor`, `roleGuard`, `Login`, `DeviceSetup`, `StaffLogin`.

- [ ] **Step 5: Manual end-to-end smoke test in the browser**

With Postgres up, the API running (`dotnet run --project src/RestaurantEmpire.Api`), and the Angular dev server running (`npm start` in `frontend/`, proxying `/api` per `proxy.conf.json`):

1. Navigate to `http://localhost:4200/admin` while logged out. Expected: redirected to `/login` (guard denies access with no current user).
2. Navigate to `http://localhost:4200/login`, sign in as `corporate@donpicaso.dev` / `Password123!`. Expected: redirected to `/admin`, placeholder page renders.
3. Reload the page. Expected: `currentUser` is cleared (access token was only in memory) but no crash; navigating back to `/admin` redirects to `/login` — this is expected behavior for this phase (silent-refresh-on-boot is not in scope; the interceptor only refreshes reactively, after a 401).
4. Navigate to `http://localhost:4200/device-setup`, enter the seeded branch's ID (fetch it via the manual `/me` check from Task 9's Step 5, or a manager login), save. Expected: redirected to `/staff-login`.
5. On `/staff-login`, see "Staff Member" in the roster, tap it, enter PIN `1234`. Expected: redirected to `/pos`, placeholder page renders.
6. Enter a wrong PIN. Expected: "Incorrect PIN." error shown, PIN pad clears, no navigation.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/app
git commit -m "Wire login/staff-login routes, auth interceptor, and guarded placeholders"
```

---

## Self-Review Notes

- **Spec coverage:** every in-scope item from the design spec has a task — module structure (Task 1), data model (Tasks 2-3), auth flows (Tasks 5-9), authorization (Task 4), frontend (Tasks 10-15), error handling (generic 401s in Tasks 5/6, interceptor redirect in Task 11, device-not-configured redirect in Task 14), testing (a test step in every task except the two infra/manual ones, which use manual verification instead, matching the `Modules.Sales` precedent for migrations).
- **Placeholder scan:** no `TBD`/`TODO` remain; the two intentional UI placeholders (`AdminPlaceholder`/`PosPlaceholder`, Task 15) are justified stand-ins for out-of-scope future sub-projects, not unfinished work in this plan.
- **Type consistency:** `LoginResult`/`LoginResponse` (Task 5) are reused verbatim by Tasks 6-7 rather than redefined. `Role`/`CurrentUser` (Task 10) match the JWT claim shape produced by `JwtTokenService` (Task 4) exactly (`sub`, `role`, `brandId`, `branchId`). `roleGuard`'s `ROLE_RANK` (Task 12) mirrors the backend `RoleAuthorizationHandler`'s `UserRole` enum ordering (Task 4) exactly.

