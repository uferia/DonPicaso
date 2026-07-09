# POS Ordering UI + Menu Catalog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the staff-facing POS ordering screen (menu grid + cart + cash/card payment) backed by a new backend menu catalog, plus logout affordances and a PrimeNG-redesigned login experience.

**Architecture:** A new `Modules.Menu` vertical-slice module (own `MenuDbContext`, `menu` schema) exposes `GET /api/v1/menu`; `Modules.Sales`' `Order` is extended with discount/tax/payment fields validated server-side. The Angular POS is a `PosShell` composed of `ProductCatalog`, `CartPanel`, and `PaymentDialog` components over two services: `MenuService` (HTTP + localStorage offline cache) and `CartService` (pure signal-based money math). Orders flow through the existing offline-first `OrderSyncService` unchanged in behavior, with an extended payload.

**Tech Stack:** .NET 10 minimal APIs, EF Core 10 + Npgsql, FluentValidation 12, MSTest/FluentAssertions/Moq; Angular 21 standalone + signals, PrimeNG v21 + `@primeuix/themes` (Aura preset) + primeicons, Dexie, Vitest.

**Spec:** `docs/superpowers/specs/2026-07-09-pos-ui-menu-catalog-design.md`

## Global Constraints

- Backend package versions (match existing csproj files exactly): `Microsoft.EntityFrameworkCore` 10.0.9, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.2, `FluentValidation.DependencyInjectionExtensions` 12.1.1, `FluentAssertions` 6.12.2, `Moq` 4.20.72, `MSTest` 4.0.2, `Microsoft.EntityFrameworkCore.InMemory` 10.0.9. Target `net10.0`.
- Database: explicit snake_case column/table/index names, one schema per module (`menu` for the new module), `numeric(12,2)` for money, `numeric(5,2)` for percentages, `timestamptz` for timestamps.
- All API routes are prefixed `/api/v1`.
- Rounding rule (spec): **half-up to 2 decimals** — C#: `Math.Round(value, 2, MidpointRounding.AwayFromZero)`; TypeScript: `Math.round((value + Number.EPSILON) * 100) / 100`. Applied identically on both sides so client-computed orders always pass server validation.
- Menu tax rate comes from configuration key `Menu:TaxRatePercent` (value `1.5` in dev appsettings), never per-branch.
- Commits: plain imperative messages matching repo history (e.g. "Add Modules.Menu catalog module"), **no `Co-Authored-By` trailer ever** (CLAUDE.md rule).
- Frontend: standalone components, signals, template-driven forms (`FormsModule`), SCSS files per component, Prettier printWidth 100 / singleQuote.
- PrimeNG scope: POS screens + login/staff-login + the new logout button only. Do NOT restyle existing admin list/form pages.
- Migrations require the dev database: run `docker compose up -d` first. Migration commands always pass `--startup-project src/RestaurantEmpire.Api`.
- All commands below run from the repo root `c:\Projects\DonPicaso` unless a `cd frontend` is shown.

---

### Task 1: Scaffold Modules.Menu (project, entities, DbContext, wiring, migration)

Infrastructure task — no behavior yet, so the verification is compilation + a generated migration rather than a unit test.

**Files:**
- Create: `src/Modules/Modules.Menu/Modules.Menu.csproj`
- Create: `src/Modules/Modules.Menu/Features/Catalog/Category.cs`
- Create: `src/Modules/Modules.Menu/Features/Catalog/Product.cs`
- Create: `src/Modules/Modules.Menu/Features/Catalog/CategoryEntityConfiguration.cs`
- Create: `src/Modules/Modules.Menu/Features/Catalog/ProductEntityConfiguration.cs`
- Create: `src/Modules/Modules.Menu/Persistence/MenuDbContext.cs`
- Create: `src/Modules/Modules.Menu/MenuOptions.cs`
- Create: `src/Modules/Modules.Menu/MenuModule.cs`
- Modify: `src/RestaurantEmpire.Api/RestaurantEmpire.Api.csproj` (add project reference)
- Modify: `src/RestaurantEmpire.Api/Program.cs` (register module)
- Modify: `src/RestaurantEmpire.Api/appsettings.json` (MenuDb connection string + Menu section)

**Interfaces:**
- Consumes: nothing new.
- Produces: `Category.Create(Guid brandId, string name, int displayOrder)`, `Product.Create(Guid brandId, Guid categoryId, string name, decimal price, int displayOrder, string? imageUrl = null)`, `MenuDbContext` (DbSets `Categories`, `Products`), `MenuOptions(decimal TaxRatePercent)`, `services.AddMenuModule(string connectionString, MenuOptions options)`, `app.MapMenuModule()`. Task 2 registers the GetMenu endpoint inside `MapMenuModule`.

- [ ] **Step 1: Create the project file**

`src/Modules/Modules.Menu/Modules.Menu.csproj` (mirrors Modules.Sales.csproj):

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

- [ ] **Step 2: Add the project to the solution**

```bash
dotnet sln add src/Modules/Modules.Menu/Modules.Menu.csproj --solution-folder src/Modules
```

Expected: `Project 'src\Modules\Modules.Menu\Modules.Menu.csproj' added to the solution.`

- [ ] **Step 3: Create the entities**

`src/Modules/Modules.Menu/Features/Catalog/Category.cs`:

```csharp
namespace Modules.Menu.Features.Catalog;

/// <summary>
/// A brand-scoped menu section (Coffee, Snacks, ...). The tenancy model shares
/// a Brand's menu across all of its Branches, so there is no BranchId here.
/// </summary>
public sealed class Category
{
    public Guid Id { get; private set; }

    public Guid BrandId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    private Category()
    {
        // EF Core materialization.
    }

    public static Category Create(Guid brandId, string name, int displayOrder) =>
        new()
        {
            Id = Guid.NewGuid(),
            BrandId = brandId,
            Name = name,
            DisplayOrder = displayOrder,
            IsActive = true,
        };
}
```

`src/Modules/Modules.Menu/Features/Catalog/Product.cs`:

```csharp
namespace Modules.Menu.Features.Catalog;

public sealed class Product
{
    public Guid Id { get; private set; }

    public Guid CategoryId { get; private set; }

    /// <summary>
    /// Denormalized from Category so brand-scoped product queries never join.
    /// </summary>
    public Guid BrandId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    /// <summary>
    /// Null until real image storage exists (deferred); the POS renders a
    /// styled initials placeholder when absent.
    /// </summary>
    public string? ImageUrl { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    private Product()
    {
        // EF Core materialization.
    }

    public static Product Create(
        Guid brandId, Guid categoryId, string name, decimal price, int displayOrder, string? imageUrl = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            BrandId = brandId,
            CategoryId = categoryId,
            Name = name,
            Price = price,
            ImageUrl = imageUrl,
            DisplayOrder = displayOrder,
            IsActive = true,
        };
}
```

- [ ] **Step 4: Create the entity configurations**

`src/Modules/Modules.Menu/Features/Catalog/CategoryEntityConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Modules.Menu.Features.Catalog;

internal sealed class CategoryEntityConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(c => c.Id).HasName("pk_categories");

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.BrandId).HasColumnName("brand_id").IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(c => c.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasIndex(c => c.BrandId).HasDatabaseName("ix_categories_brand_id");
    }
}
```

`src/Modules/Modules.Menu/Features/Catalog/ProductEntityConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Modules.Menu.Features.Catalog;

internal sealed class ProductEntityConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id).HasName("pk_products");

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.CategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(p => p.BrandId).HasColumnName("brand_id").IsRequired();
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(p => p.Price).HasColumnName("price").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(p => p.ImageUrl).HasColumnName("image_url").HasMaxLength(2000);
        builder.Property(p => p.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(p => p.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .HasConstraintName("fk_products_category_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.CategoryId).HasDatabaseName("ix_products_category_id");
        builder.HasIndex(p => p.BrandId).HasDatabaseName("ix_products_brand_id");
    }
}
```

- [ ] **Step 5: Create the DbContext, options, and module composition root**

`src/Modules/Modules.Menu/Persistence/MenuDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Modules.Menu.Features.Catalog;

namespace Modules.Menu.Persistence;

public sealed class MenuDbContext(DbContextOptions<MenuDbContext> options) : DbContext(options)
{
    public const string Schema = "menu";

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MenuDbContext).Assembly);
    }
}
```

`src/Modules/Modules.Menu/MenuOptions.cs`:

```csharp
namespace Modules.Menu;

/// <summary>
/// Bound from the "Menu" configuration section by the host. A single
/// tax rate for now — per-branch tax configuration is explicitly deferred.
/// </summary>
public sealed record MenuOptions(decimal TaxRatePercent);
```

`src/Modules/Modules.Menu/MenuModule.cs`:

```csharp
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Menu.Persistence;

namespace Modules.Menu;

/// <summary>
/// Composition root for the Menu module. The host (API bootstrap project)
/// calls these two methods; everything else stays internal to the module.
/// </summary>
public static class MenuModule
{
    public static IServiceCollection AddMenuModule(
        this IServiceCollection services, string connectionString, MenuOptions options)
    {
        services.AddDbContext<MenuDbContext>(o => o.UseNpgsql(connectionString));
        services.AddSingleton(options);

        return services;
    }

    public static IEndpointRouteBuilder MapMenuModule(this IEndpointRouteBuilder app)
    {
        return app;
    }
}
```

- [ ] **Step 6: Wire the module into the host**

`src/RestaurantEmpire.Api/RestaurantEmpire.Api.csproj` — add to the existing `<ItemGroup>` with project references:

```xml
    <ProjectReference Include="..\Modules\Modules.Menu\Modules.Menu.csproj" />
```

`src/RestaurantEmpire.Api/appsettings.json` — add `"MenuDb"` inside `ConnectionStrings` and a `"Menu"` section after `"Jwt"` (same database as the other modules; schema separation happens inside EF):

```json
    "MenuDb": "Host=localhost;Port=5432;Database=restaurant_empire;Username=postgres;Password=postgres"
```

```json
  "Menu": {
    "TaxRatePercent": 1.5
  }
```

`src/RestaurantEmpire.Api/Program.cs` — add the using and register the module directly after the `AddSalesModule` call:

```csharp
using Modules.Menu;
```

```csharp
builder.Services.AddMenuModule(
    builder.Configuration.GetConnectionString("MenuDb")
        ?? throw new InvalidOperationException("Connection string 'MenuDb' is not configured."),
    builder.Configuration.GetSection("Menu").Get<MenuOptions>()
        ?? throw new InvalidOperationException("'Menu' configuration section is not configured."));
```

And after `app.MapSalesModule();`:

```csharp
app.MapMenuModule();
```

- [ ] **Step 7: Build**

Run: `dotnet build`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 8: Create and apply the initial migration**

```bash
docker compose up -d
dotnet tool restore
dotnet ef migrations add InitialMenuSchema --project src/Modules/Modules.Menu --startup-project src/RestaurantEmpire.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/Modules/Modules.Menu --startup-project src/RestaurantEmpire.Api
```

Expected: migration files appear under `src/Modules/Modules.Menu/Persistence/Migrations/` (the `--output-dir` matches Sales' layout), and `database update` ends with `Done.`

- [ ] **Step 9: Commit**

```bash
git add src/Modules/Modules.Menu src/RestaurantEmpire.Api RestaurantEmpire.sln
git commit -m "Add Modules.Menu catalog module with Category/Product schema"
```

---

### Task 2: GetMenu query + endpoint (TDD)

**Files:**
- Create: `tests/Modules.Menu.Tests/Modules.Menu.Tests.csproj`
- Create: `tests/Modules.Menu.Tests/MSTestSettings.cs`
- Create: `tests/Modules.Menu.Tests/Features/Catalog/GetMenu/GetMenuQueryHandlerTests.cs`
- Create: `src/Modules/Modules.Menu/Features/Catalog/GetMenu/GetMenuQueryHandler.cs`
- Create: `src/Modules/Modules.Menu/Features/Catalog/GetMenu/GetMenuEndpoint.cs`
- Modify: `src/Modules/Modules.Menu/MenuModule.cs` (register handler + map endpoint)

**Interfaces:**
- Consumes: `MenuDbContext`, `Category.Create`, `Product.Create`, `MenuOptions` (Task 1).
- Produces: `GetMenuQueryHandler.HandleAsync(Guid brandId, CancellationToken) -> Task<MenuResult>`; records `MenuResult(IReadOnlyList<MenuCategoryResult> Categories, decimal TaxRatePercent)`, `MenuCategoryResult(Guid Id, string Name, IReadOnlyList<MenuProductResult> Products)`, `MenuProductResult(Guid Id, string Name, decimal Price, string? ImageUrl)`; `GET /api/v1/menu` returning that shape camelCased. Task 6's Angular `MenuResponse` mirrors this JSON.

- [ ] **Step 1: Create the test project**

`tests/Modules.Menu.Tests/Modules.Menu.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

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
    <ProjectReference Include="..\..\src\Modules\Modules.Menu\Modules.Menu.csproj" />
  </ItemGroup>

</Project>
```

`tests/Modules.Menu.Tests/MSTestSettings.cs`:

```csharp
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
```

```bash
dotnet sln add tests/Modules.Menu.Tests/Modules.Menu.Tests.csproj --solution-folder tests
```

- [ ] **Step 2: Write the failing tests**

`tests/Modules.Menu.Tests/Features/Catalog/GetMenu/GetMenuQueryHandlerTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Menu;
using Modules.Menu.Features.Catalog;
using Modules.Menu.Features.Catalog.GetMenu;
using Modules.Menu.Persistence;

namespace Modules.Menu.Tests.Features.Catalog.GetMenu;

[TestClass]
public sealed class GetMenuQueryHandlerTests
{
    private MenuDbContext _dbContext = null!;
    private GetMenuQueryHandler _handler = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<MenuDbContext>()
            .UseInMemoryDatabase($"menu-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new MenuDbContext(options);
        _handler = new GetMenuQueryHandler(_dbContext, new MenuOptions(TaxRatePercent: 1.5m));
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_ReturnsActiveCategoriesAndProductsForBrandInDisplayOrder()
    {
        var brandId = Guid.NewGuid();

        var snacks = Category.Create(brandId, "Snacks", displayOrder: 2);
        var coffee = Category.Create(brandId, "Coffee", displayOrder: 1);
        var espresso = Product.Create(brandId, coffee.Id, "Espresso", 2.50m, displayOrder: 2);
        var latte = Product.Create(brandId, coffee.Id, "Caffe Latte", 4.25m, displayOrder: 1);

        _dbContext.Categories.AddRange(snacks, coffee);
        _dbContext.Products.AddRange(espresso, latte);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(brandId, CancellationToken.None);

        result.TaxRatePercent.Should().Be(1.5m);
        result.Categories.Select(c => c.Name).Should().ContainInOrder("Coffee", "Snacks");
        result.Categories[0].Products.Select(p => p.Name).Should().ContainInOrder("Caffe Latte", "Espresso");
        result.Categories[0].Products[1].Price.Should().Be(2.50m);
        result.Categories[1].Products.Should().BeEmpty();
    }

    [TestMethod]
    public async Task HandleAsync_ExcludesInactiveEntriesAndOtherBrands()
    {
        var brandId = Guid.NewGuid();
        var otherBrandId = Guid.NewGuid();

        var coffee = Category.Create(brandId, "Coffee", 1);
        var inactiveCategory = Category.Create(brandId, "Retired Section", 2);
        typeof(Category).GetProperty(nameof(Category.IsActive))!.SetValue(inactiveCategory, false);

        var activeProduct = Product.Create(brandId, coffee.Id, "Espresso", 2.50m, 1);
        var inactiveProduct = Product.Create(brandId, coffee.Id, "Retired Drink", 1.00m, 2);
        typeof(Product).GetProperty(nameof(Product.IsActive))!.SetValue(inactiveProduct, false);

        var foreignCategory = Category.Create(otherBrandId, "Foreign Menu", 1);

        _dbContext.Categories.AddRange(coffee, inactiveCategory, foreignCategory);
        _dbContext.Products.AddRange(activeProduct, inactiveProduct);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(brandId, CancellationToken.None);

        result.Categories.Should().HaveCount(1);
        result.Categories[0].Name.Should().Be("Coffee");
        result.Categories[0].Products.Should().HaveCount(1);
        result.Categories[0].Products[0].Name.Should().Be("Espresso");
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Modules.Menu.Tests`
Expected: compile error — `GetMenuQueryHandler` does not exist.

- [ ] **Step 4: Implement the handler**

`src/Modules/Modules.Menu/Features/Catalog/GetMenu/GetMenuQueryHandler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Modules.Menu.Persistence;

namespace Modules.Menu.Features.Catalog.GetMenu;

/// <summary>
/// Read model for the POS ordering screen: the caller's brand menu plus the
/// tax rate the cart must apply. Two set-based queries, grouped in memory —
/// menus are small (tens of rows), so no join gymnastics.
/// </summary>
public sealed class GetMenuQueryHandler(MenuDbContext dbContext, MenuOptions options)
{
    public async Task<MenuResult> HandleAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        var categories = await dbContext.Categories
            .Where(c => c.BrandId == brandId && c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken);

        var products = await dbContext.Products
            .Where(p => p.BrandId == brandId && p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(cancellationToken);

        var categoryResults = categories
            .Select(c => new MenuCategoryResult(
                c.Id,
                c.Name,
                products
                    .Where(p => p.CategoryId == c.Id)
                    .Select(p => new MenuProductResult(p.Id, p.Name, p.Price, p.ImageUrl))
                    .ToList()))
            .ToList();

        return new MenuResult(categoryResults, options.TaxRatePercent);
    }
}

public sealed record MenuProductResult(Guid Id, string Name, decimal Price, string? ImageUrl);

public sealed record MenuCategoryResult(Guid Id, string Name, IReadOnlyList<MenuProductResult> Products);

public sealed record MenuResult(IReadOnlyList<MenuCategoryResult> Categories, decimal TaxRatePercent);
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Modules.Menu.Tests`
Expected: `Passed! - 2 passed`

- [ ] **Step 6: Add the endpoint and register the slice**

`src/Modules/Modules.Menu/Features/Catalog/GetMenu/GetMenuEndpoint.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Modules.Menu.Features.Catalog.GetMenu;

public static class GetMenuEndpoint
{
    /// <summary>
    /// Registered by the Identity module at the host level; referenced by
    /// name so Modules.Menu doesn't take a project reference on Identity.
    /// </summary>
    private const string RequireStaffOrAbove = "RequireStaffOrAbove";

    public static IEndpointRouteBuilder MapGetMenu(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/menu", async (
                ClaimsPrincipal user,
                GetMenuQueryHandler handler,
                CancellationToken cancellationToken) =>
            {
                // The brand always comes from the token, never the client —
                // same trust model as staff login. Corporate users carry no
                // brandId claim: the POS menu only makes sense in a
                // brand-scoped session.
                var brandClaim = user.FindFirstValue("brandId");
                if (!Guid.TryParse(brandClaim, out var brandId))
                {
                    return Results.Forbid();
                }

                return Results.Ok(await handler.HandleAsync(brandId, cancellationToken));
            })
            .RequireAuthorization(RequireStaffOrAbove)
            .WithName("GetMenu")
            .WithTags("Menu")
            .Produces<MenuResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
```

`src/Modules/Modules.Menu/MenuModule.cs` — add the using, handler registration, and mapping:

```csharp
using Modules.Menu.Features.Catalog.GetMenu;
```

In `AddMenuModule`, after `services.AddSingleton(options);`:

```csharp
        services.AddScoped<GetMenuQueryHandler>();
```

In `MapMenuModule`, before `return app;`:

```csharp
        app.MapGetMenu();
```

- [ ] **Step 7: Full build + all backend tests**

Run: `dotnet build && dotnet test`
Expected: build succeeds; all test projects pass.

- [ ] **Step 8: Commit**

```bash
git add src/Modules/Modules.Menu tests/Modules.Menu.Tests RestaurantEmpire.sln
git commit -m "Add GetMenu query and brand-scoped menu endpoint"
```

---

### Task 3: MenuSeeder + dev seeding hook (TDD)

**Files:**
- Create: `tests/Modules.Menu.Tests/Persistence/MenuSeederTests.cs`
- Create: `src/Modules/Modules.Menu/Persistence/MenuSeeder.cs`
- Modify: `src/RestaurantEmpire.Api/Program.cs` (seed after IdentitySeeder)

**Interfaces:**
- Consumes: `MenuDbContext`, `Category.Create`, `Product.Create` (Task 1); `IdentityDbContext.Brands` (existing) — bridged only in Program.cs, the host composition root, so the modules stay decoupled.
- Produces: `MenuSeeder.SeedAsync(MenuDbContext dbContext, Guid brandId)`.

- [ ] **Step 1: Write the failing tests**

`tests/Modules.Menu.Tests/Persistence/MenuSeederTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Menu.Persistence;

namespace Modules.Menu.Tests.Persistence;

[TestClass]
public sealed class MenuSeederTests
{
    private MenuDbContext _dbContext = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<MenuDbContext>()
            .UseInMemoryDatabase($"menu-seeder-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new MenuDbContext(options);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task SeedAsync_PopulatesCategoriesAndProductsForTheBrand()
    {
        var brandId = Guid.NewGuid();

        await MenuSeeder.SeedAsync(_dbContext, brandId);

        var categories = await _dbContext.Categories.ToListAsync();
        var products = await _dbContext.Products.ToListAsync();

        categories.Should().NotBeEmpty();
        categories.Should().OnlyContain(c => c.BrandId == brandId && c.IsActive);
        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.BrandId == brandId && p.IsActive && p.Price > 0);
        products.Select(p => p.CategoryId).Distinct()
            .Should().BeSubsetOf(categories.Select(c => c.Id));
    }

    [TestMethod]
    public async Task SeedAsync_IsIdempotent()
    {
        var brandId = Guid.NewGuid();

        await MenuSeeder.SeedAsync(_dbContext, brandId);
        var countAfterFirstRun = await _dbContext.Products.CountAsync();

        await MenuSeeder.SeedAsync(_dbContext, brandId);

        (await _dbContext.Products.CountAsync()).Should().Be(countAfterFirstRun);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Modules.Menu.Tests`
Expected: compile error — `MenuSeeder` does not exist.

- [ ] **Step 3: Implement the seeder**

`src/Modules/Modules.Menu/Persistence/MenuSeeder.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Modules.Menu.Features.Catalog;

namespace Modules.Menu.Persistence;

/// <summary>
/// Seeds a sample menu for the given brand so the POS ordering screen is
/// exercisable before menu admin CRUD exists (a deferred sub-project).
/// Dev/test convenience only — not for production use. ImageUrl stays null:
/// the POS renders initials placeholders until image storage exists.
/// </summary>
public static class MenuSeeder
{
    public static async Task SeedAsync(MenuDbContext dbContext, Guid brandId)
    {
        if (await dbContext.Categories.AnyAsync())
        {
            return;
        }

        var coffee = Category.Create(brandId, "Coffee", 1);
        var beverages = Category.Create(brandId, "Beverages", 2);
        var snacks = Category.Create(brandId, "Snacks", 3);
        var desserts = Category.Create(brandId, "Desserts", 4);

        Product[] products =
        [
            Product.Create(brandId, coffee.Id, "Espresso", 2.50m, 1),
            Product.Create(brandId, coffee.Id, "Cappuccino", 3.75m, 2),
            Product.Create(brandId, coffee.Id, "Caffe Latte", 4.25m, 3),
            Product.Create(brandId, coffee.Id, "Mocha", 4.75m, 4),
            Product.Create(brandId, beverages.Id, "Fresh Orange Juice", 3.50m, 1),
            Product.Create(brandId, beverages.Id, "Iced Tea", 2.75m, 2),
            Product.Create(brandId, beverages.Id, "Sparkling Water", 2.00m, 3),
            Product.Create(brandId, snacks.Id, "Club Sandwich", 6.50m, 1),
            Product.Create(brandId, snacks.Id, "Quesadilla", 5.95m, 2),
            Product.Create(brandId, snacks.Id, "French Fries", 3.25m, 3),
            Product.Create(brandId, desserts.Id, "Tiramisu", 5.50m, 1),
            Product.Create(brandId, desserts.Id, "Cheesecake", 5.25m, 2),
        ];

        dbContext.Categories.AddRange(coffee, beverages, snacks, desserts);
        dbContext.Products.AddRange(products);

        await dbContext.SaveChangesAsync();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Modules.Menu.Tests`
Expected: `Passed!` (4 tests total in this project now).

- [ ] **Step 5: Hook into dev seeding in Program.cs**

Add usings to `src/RestaurantEmpire.Api/Program.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Modules.Menu.Persistence;
```

Extend the existing `if (app.Environment.IsDevelopment())` block — after the `IdentitySeeder.SeedAsync(...)` call, inside the same `seedScope`:

```csharp
    // The menu seeder needs the seeded brand's id. Only the host may bridge
    // the two modules' contexts — they never reference each other.
    var seedBrandId = await seedScope.ServiceProvider.GetRequiredService<IdentityDbContext>()
        .Brands.Select(b => b.Id).FirstAsync();

    await MenuSeeder.SeedAsync(
        seedScope.ServiceProvider.GetRequiredService<MenuDbContext>(),
        seedBrandId);
```

- [ ] **Step 6: Verify end-to-end**

Run: `dotnet build && dotnet test`
Expected: all pass.

Then smoke-check the endpoint manually (requires docker db up):

```bash
dotnet run --project src/RestaurantEmpire.Api &
sleep 8
TOKEN=$(curl -s -X POST http://localhost:5098/api/v1/auth/login -H "Content-Type: application/json" -d '{"email":"manager@donpicaso.dev","password":"Password123!"}' | python -c "import sys,json;print(json.load(sys.stdin)['accessToken'])")
curl -s http://localhost:5098/api/v1/menu -H "Authorization: Bearer $TOKEN"
kill %1
```

Expected: JSON with `"categories":[{"id":...,"name":"Coffee","products":[...]}...],"taxRatePercent":1.5`.

- [ ] **Step 7: Commit**

```bash
git add src/Modules/Modules.Menu tests/Modules.Menu.Tests src/RestaurantEmpire.Api/Program.cs
git commit -m "Seed sample menu data in development"
```

---

### Task 4: Extend Order with discount/tax/payment fields (TDD)

**Files:**
- Create: `src/Modules/Modules.Sales/Features/Orders/PaymentMethod.cs`
- Create: `tests/Modules.Sales.Tests/Features/Orders/CreateOrder/CreateOrderCommandValidatorTests.cs`
- Modify: `src/Modules/Modules.Sales/Features/Orders/CreateOrder/CreateOrderCommand.cs`
- Modify: `src/Modules/Modules.Sales/Features/Orders/CreateOrder/CreateOrderCommandValidator.cs`
- Modify: `src/Modules/Modules.Sales/Features/Orders/Order.cs`
- Modify: `src/Modules/Modules.Sales/Features/Orders/CreateOrder/CreateOrderCommandHandler.cs`
- Modify: `src/Modules/Modules.Sales/Features/Orders/CreateOrder/OrderEntityConfiguration.cs`
- Modify: `src/RestaurantEmpire.Api/Program.cs` (string enum JSON converter)
- Modify: `tests/Modules.Sales.Tests/Features/Orders/CreateOrder/CreateOrderCommandHandlerTests.cs`

**Interfaces:**
- Consumes: existing CreateOrder slice.
- Produces: `PaymentMethod` enum (`Cash`, `Card` — serialized as strings over HTTP); new `CreateOrderCommand(Guid ClientOrderId, Guid BranchId, Guid BrandId, decimal Subtotal, decimal DiscountPercent, decimal DiscountAmount, decimal TaxRatePercent, decimal TaxAmount, decimal TotalAmount, PaymentMethod PaymentMethod, decimal? CashTendered, decimal? ChangeDue, IReadOnlyList<OrderItemDto> Items)`. Task 10's frontend payload mirrors this shape camelCased with `paymentMethod: 'Cash' | 'Card'`.

- [ ] **Step 1: Update the command record and add the enum (compile-driven change — the updated tests are the failing tests)**

`src/Modules/Modules.Sales/Features/Orders/PaymentMethod.cs`:

```csharp
namespace Modules.Sales.Features.Orders;

/// <summary>
/// How the customer settled the order. "Card" records the method only —
/// actual card processing is explicitly out of scope.
/// </summary>
public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
}
```

`src/Modules/Modules.Sales/Features/Orders/CreateOrder/CreateOrderCommand.cs` — replace the command record (leave `OrderItemDto` unchanged):

```csharp
public sealed record CreateOrderCommand(
    Guid ClientOrderId,
    Guid BranchId,
    Guid BrandId,
    decimal Subtotal,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal TaxRatePercent,
    decimal TaxAmount,
    decimal TotalAmount,
    PaymentMethod PaymentMethod,
    decimal? CashTendered,
    decimal? ChangeDue,
    IReadOnlyList<OrderItemDto> Items);
```

- [ ] **Step 2: Write the failing validator tests**

`tests/Modules.Sales.Tests/Features/Orders/CreateOrder/CreateOrderCommandValidatorTests.cs`:

```csharp
using FluentAssertions;
using Modules.Sales.Features.Orders;
using Modules.Sales.Features.Orders.CreateOrder;

namespace Modules.Sales.Tests.Features.Orders.CreateOrder;

[TestClass]
public sealed class CreateOrderCommandValidatorTests
{
    private readonly CreateOrderCommandValidator _validator = new();

    // 2 × 12.50 + 1 × 10.00 = 35.00 subtotal; 10% discount = 3.50;
    // 1.5% tax on 31.50 = 0.4725 -> 0.47 (half-up); total 31.97;
    // cash 40.00 tendered -> 8.03 change.
    private static CreateOrderCommand BuildValidCashCommand() =>
        new(
            ClientOrderId: Guid.NewGuid(),
            BranchId: Guid.NewGuid(),
            BrandId: Guid.NewGuid(),
            Subtotal: 35.00m,
            DiscountPercent: 10m,
            DiscountAmount: 3.50m,
            TaxRatePercent: 1.5m,
            TaxAmount: 0.47m,
            TotalAmount: 31.97m,
            PaymentMethod: PaymentMethod.Cash,
            CashTendered: 40.00m,
            ChangeDue: 8.03m,
            Items:
            [
                new OrderItemDto(Guid.NewGuid(), "Margherita Pizza", Quantity: 2, UnitPrice: 12.50m),
                new OrderItemDto(Guid.NewGuid(), "Tiramisu", Quantity: 1, UnitPrice: 10.00m),
            ]);

    [TestMethod]
    public void Validate_WithConsistentCashCommand_Passes()
    {
        _validator.Validate(BuildValidCashCommand()).IsValid.Should().BeTrue();
    }

    [TestMethod]
    public void Validate_WithConsistentCardCommand_Passes()
    {
        var command = BuildValidCashCommand() with
        {
            PaymentMethod = PaymentMethod.Card,
            CashTendered = null,
            ChangeDue = null,
        };

        _validator.Validate(command).IsValid.Should().BeTrue();
    }

    [TestMethod]
    public void Validate_RoundsDiscountHalfUp()
    {
        // 1 × 12.35 subtotal; 10% discount = 1.235 -> 1.24 (half-up);
        // 1.5% tax on 11.11 = 0.16665 -> 0.17; total 11.28.
        var command = BuildValidCashCommand() with
        {
            Subtotal = 12.35m,
            DiscountPercent = 10m,
            DiscountAmount = 1.24m,
            TaxAmount = 0.17m,
            TotalAmount = 11.28m,
            CashTendered = 20.00m,
            ChangeDue = 8.72m,
            Items = [new OrderItemDto(Guid.NewGuid(), "Combo Plate", Quantity: 1, UnitPrice: 12.35m)],
        };

        _validator.Validate(command).IsValid.Should().BeTrue();

        var truncatedInsteadOfRounded = command with { DiscountAmount = 1.23m };
        _validator.Validate(truncatedInsteadOfRounded).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.DiscountAmount));
    }

    [TestMethod]
    public void Validate_WhenSubtotalDoesNotMatchItems_Fails()
    {
        var command = BuildValidCashCommand() with { Subtotal = 99.99m };

        _validator.Validate(command).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.Subtotal));
    }

    [TestMethod]
    public void Validate_WhenTaxAmountIsWrong_Fails()
    {
        var command = BuildValidCashCommand() with { TaxAmount = 1.00m };

        _validator.Validate(command).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.TaxAmount));
    }

    [TestMethod]
    public void Validate_WhenTotalIsNotSubtotalMinusDiscountPlusTax_Fails()
    {
        var command = BuildValidCashCommand() with { TotalAmount = 35.00m, CashTendered = 40.00m, ChangeDue = 5.00m };

        _validator.Validate(command).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.TotalAmount));
    }

    [TestMethod]
    public void Validate_CashWithoutTenderedAmount_Fails()
    {
        var command = BuildValidCashCommand() with { CashTendered = null, ChangeDue = null };

        _validator.Validate(command).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.CashTendered));
    }

    [TestMethod]
    public void Validate_CashTenderedBelowTotal_Fails()
    {
        var command = BuildValidCashCommand() with { CashTendered = 30.00m, ChangeDue = -1.97m };

        _validator.Validate(command).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.CashTendered));
    }

    [TestMethod]
    public void Validate_CashWithWrongChange_Fails()
    {
        var command = BuildValidCashCommand() with { ChangeDue = 9.00m };

        _validator.Validate(command).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.ChangeDue));
    }

    [TestMethod]
    public void Validate_CardWithCashFields_Fails()
    {
        var command = BuildValidCashCommand() with { PaymentMethod = PaymentMethod.Card };

        var errors = _validator.Validate(command).Errors.Select(e => e.PropertyName);

        errors.Should().Contain(nameof(CreateOrderCommand.CashTendered));
        errors.Should().Contain(nameof(CreateOrderCommand.ChangeDue));
    }

    [TestMethod]
    public void Validate_DiscountPercentOutOfRange_Fails()
    {
        var command = BuildValidCashCommand() with { DiscountPercent = 101m };

        _validator.Validate(command).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.DiscountPercent));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Modules.Sales.Tests`
Expected: compile errors (old command shape everywhere) — that's the red state for this type-driven change.

- [ ] **Step 4: Rewrite the validator**

`src/Modules/Modules.Sales/Features/Orders/CreateOrder/CreateOrderCommandValidator.cs` — full replacement:

```csharp
using FluentValidation;

namespace Modules.Sales.Features.Orders.CreateOrder;

/// <summary>
/// The client (POS cart) computes the money breakdown; this validator
/// re-derives every figure and rejects any drift. Rounding is half-up to
/// 2 decimals, mirrored exactly by roundMoney() in the Angular cart.
/// </summary>
public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(c => c.ClientOrderId)
            .NotEmpty()
            .WithMessage("ClientOrderId is required (device-generated idempotency key).");

        RuleFor(c => c.BranchId)
            .NotEmpty()
            .WithMessage("BranchId is required.");

        RuleFor(c => c.BrandId)
            .NotEmpty()
            .WithMessage("BrandId is required.");

        RuleFor(c => c.Items)
            .NotEmpty()
            .WithMessage("An order must contain at least one item.");

        RuleForEach(c => c.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId)
                .NotEmpty()
                .WithMessage("ProductId is required.");

            item.RuleFor(i => i.ProductName)
                .NotEmpty()
                .MaximumLength(200);

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0)
                .WithMessage("Quantity must be a positive number.");

            item.RuleFor(i => i.UnitPrice)
                .GreaterThanOrEqualTo(0)
                .WithMessage("UnitPrice cannot be negative.");
        });

        RuleFor(c => c.Subtotal)
            .Must((command, subtotal) => subtotal == command.Items.Sum(i => i.Quantity * i.UnitPrice))
            .When(c => c.Items is { Count: > 0 })
            .WithMessage("Subtotal must equal the sum of Quantity * UnitPrice across all items.");

        RuleFor(c => c.DiscountPercent)
            .InclusiveBetween(0m, 100m)
            .WithMessage("DiscountPercent must be between 0 and 100.");

        RuleFor(c => c.DiscountAmount)
            .Must((c, discount) => discount == RoundMoney(c.Subtotal * c.DiscountPercent / 100m))
            .WithMessage("DiscountAmount must equal Subtotal * DiscountPercent / 100, rounded half-up to 2 decimals.");

        RuleFor(c => c.TaxRatePercent)
            .InclusiveBetween(0m, 100m)
            .WithMessage("TaxRatePercent must be between 0 and 100.");

        RuleFor(c => c.TaxAmount)
            .Must((c, tax) => tax == RoundMoney((c.Subtotal - c.DiscountAmount) * c.TaxRatePercent / 100m))
            .WithMessage("TaxAmount must equal (Subtotal - DiscountAmount) * TaxRatePercent / 100, rounded half-up to 2 decimals.");

        RuleFor(c => c.TotalAmount)
            .GreaterThan(0)
            .WithMessage("TotalAmount must be positive.");

        RuleFor(c => c.TotalAmount)
            .Must((c, total) => total == c.Subtotal - c.DiscountAmount + c.TaxAmount)
            .WithMessage("TotalAmount must equal Subtotal - DiscountAmount + TaxAmount.");

        RuleFor(c => c.PaymentMethod)
            .IsInEnum()
            .WithMessage("PaymentMethod must be Cash or Card.");

        When(c => c.PaymentMethod == PaymentMethod.Cash, () =>
        {
            RuleFor(c => c.CashTendered)
                .NotNull()
                .WithMessage("CashTendered is required for cash payments.")
                .GreaterThanOrEqualTo(c => c.TotalAmount)
                .WithMessage("CashTendered must cover the total.");

            RuleFor(c => c.ChangeDue)
                .NotNull()
                .WithMessage("ChangeDue is required for cash payments.")
                .Must((c, change) => change == c.CashTendered - c.TotalAmount)
                .When(c => c.CashTendered is not null, ApplyConditionTo.CurrentValidator)
                .WithMessage("ChangeDue must equal CashTendered - TotalAmount.");
        });

        When(c => c.PaymentMethod == PaymentMethod.Card, () =>
        {
            RuleFor(c => c.CashTendered)
                .Null()
                .WithMessage("CashTendered must be null for card payments.");

            RuleFor(c => c.ChangeDue)
                .Null()
                .WithMessage("ChangeDue must be null for card payments.");
        });
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
```

- [ ] **Step 5: Extend the entity, handler, and mapping**

`src/Modules/Modules.Sales/Features/Orders/Order.cs` — add properties after `TotalAmount` and replace `Create`:

```csharp
    public decimal Subtotal { get; private set; }

    public decimal DiscountPercent { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public decimal TaxRatePercent { get; private set; }

    public decimal TaxAmount { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; }

    public decimal? CashTendered { get; private set; }

    public decimal? ChangeDue { get; private set; }
```

```csharp
    public static Order Create(
        Guid clientOrderId,
        Guid branchId,
        Guid brandId,
        decimal subtotal,
        decimal discountPercent,
        decimal discountAmount,
        decimal taxRatePercent,
        decimal taxAmount,
        decimal totalAmount,
        PaymentMethod paymentMethod,
        decimal? cashTendered,
        decimal? changeDue,
        IEnumerable<OrderItem> items,
        DateTimeOffset createdAtUtc)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            ClientOrderId = clientOrderId,
            BranchId = branchId,
            BrandId = brandId,
            Subtotal = subtotal,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            TaxRatePercent = taxRatePercent,
            TaxAmount = taxAmount,
            TotalAmount = totalAmount,
            PaymentMethod = paymentMethod,
            CashTendered = cashTendered,
            ChangeDue = changeDue,
            CreatedAtUtc = createdAtUtc,
        };

        order._items.AddRange(items);
        return order;
    }
```

`src/Modules/Modules.Sales/Features/Orders/CreateOrder/CreateOrderCommandHandler.cs` — replace the `Order.Create` call:

```csharp
        var order = Order.Create(
            command.ClientOrderId,
            command.BranchId,
            command.BrandId,
            command.Subtotal,
            command.DiscountPercent,
            command.DiscountAmount,
            command.TaxRatePercent,
            command.TaxAmount,
            command.TotalAmount,
            command.PaymentMethod,
            command.CashTendered,
            command.ChangeDue,
            command.Items.Select(i => OrderItem.Create(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice)),
            timeProvider.GetUtcNow());
```

`src/Modules/Modules.Sales/Features/Orders/CreateOrder/OrderEntityConfiguration.cs` — add after the `TotalAmount` property mapping:

```csharp
        builder.Property(o => o.Subtotal)
            .HasColumnName("subtotal")
            .HasColumnType("numeric(12,2)")
            .IsRequired();

        builder.Property(o => o.DiscountPercent)
            .HasColumnName("discount_percent")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(o => o.DiscountAmount)
            .HasColumnName("discount_amount")
            .HasColumnType("numeric(12,2)")
            .IsRequired();

        builder.Property(o => o.TaxRatePercent)
            .HasColumnName("tax_rate_percent")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(o => o.TaxAmount)
            .HasColumnName("tax_amount")
            .HasColumnType("numeric(12,2)")
            .IsRequired();

        builder.Property(o => o.PaymentMethod)
            .HasColumnName("payment_method")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.CashTendered)
            .HasColumnName("cash_tendered")
            .HasColumnType("numeric(12,2)");

        builder.Property(o => o.ChangeDue)
            .HasColumnName("change_due")
            .HasColumnType("numeric(12,2)");
```

`src/RestaurantEmpire.Api/Program.cs` — add using and, right after `builder.Services.AddProblemDetails();`:

```csharp
using System.Text.Json.Serialization;
```

```csharp
// PaymentMethod (and future enums) cross the wire as strings ("Cash"),
// matching the Angular payload types.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

- [ ] **Step 6: Update the existing handler tests**

`tests/Modules.Sales.Tests/Features/Orders/CreateOrder/CreateOrderCommandHandlerTests.cs`:

Add `using Modules.Sales.Features.Orders;` to the usings. Replace `BuildValidCommand()`:

```csharp
    private static CreateOrderCommand BuildValidCommand() =>
        new(
            ClientOrderId: Guid.NewGuid(),
            BranchId: Guid.NewGuid(),
            BrandId: Guid.NewGuid(),
            Subtotal: 35.00m,
            DiscountPercent: 10m,
            DiscountAmount: 3.50m,
            TaxRatePercent: 1.5m,
            TaxAmount: 0.47m,
            TotalAmount: 31.97m,
            PaymentMethod: PaymentMethod.Cash,
            CashTendered: 40.00m,
            ChangeDue: 8.03m,
            Items:
            [
                new OrderItemDto(Guid.NewGuid(), "Margherita Pizza", Quantity: 2, UnitPrice: 12.50m),
                new OrderItemDto(Guid.NewGuid(), "Tiramisu", Quantity: 1, UnitPrice: 10.00m),
            ]);
```

In `HandleAsync_WithValidCommand_PersistsOrderAndReturnsItsId`, replace the two total assertions:

```csharp
        savedOrder.Subtotal.Should().Be(35.00m);
        savedOrder.DiscountPercent.Should().Be(10m);
        savedOrder.DiscountAmount.Should().Be(3.50m);
        savedOrder.TaxRatePercent.Should().Be(1.5m);
        savedOrder.TaxAmount.Should().Be(0.47m);
        savedOrder.TotalAmount.Should().Be(31.97m);
        savedOrder.PaymentMethod.Should().Be(PaymentMethod.Cash);
        savedOrder.CashTendered.Should().Be(40.00m);
        savedOrder.ChangeDue.Should().Be(8.03m);
        savedOrder.Items.Sum(i => i.LineTotal).Should().Be(savedOrder.Subtotal);
```

Rename `HandleAsync_WhenTotalDoesNotMatchItems_ThrowsValidationExceptionAndSavesNothing` to `HandleAsync_WhenSubtotalDoesNotMatchItems_ThrowsValidationExceptionAndSavesNothing` and change its arrange/assert:

```csharp
        var command = BuildValidCommand() with { Subtotal = 99.99m };

        var act = () => _handler.HandleAsync(command, CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.Subtotal));

        (await _dbContext.Orders.AnyAsync()).Should().BeFalse();
```

In `HandleAsync_WithEmptyBranchAndNonPositiveQuantity_ThrowsValidationException`, replace the command construction:

```csharp
        var command = BuildValidCommand() with
        {
            BranchId = Guid.Empty,
            Items = [new OrderItemDto(Guid.NewGuid(), "Quattro Formaggi", Quantity: 0, UnitPrice: 10.00m)],
        };
```

(The assertions on `BranchId` and `Items[0].Quantity` error keys stay as they are.)

- [ ] **Step 7: Run all backend tests**

Run: `dotnet test`
Expected: all pass (Sales handler tests, new validator tests, Menu tests, Identity tests).

- [ ] **Step 8: Create and apply the migration**

```bash
dotnet ef migrations add AddOrderPaymentFields --project src/Modules/Modules.Sales --startup-project src/RestaurantEmpire.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/Modules/Modules.Sales --startup-project src/RestaurantEmpire.Api
```

Expected: migration adds the nine columns to `sales.orders`; `Done.` (EF supplies `0`/`''` defaults for existing dev rows — acceptable, this is dev-stage data.)

- [ ] **Step 9: Commit**

```bash
git add src/Modules/Modules.Sales tests/Modules.Sales.Tests src/RestaurantEmpire.Api/Program.cs
git commit -m "Extend Order with discount, tax, and payment fields"
```

---

### Task 5: Install PrimeNG with a green Aura preset

**Files:**
- Create: `frontend/src/app/app-theme.ts`
- Modify: `frontend/src/app/app.config.ts`
- Modify: `frontend/angular.json` (primeicons stylesheet)
- Modify: `frontend/package.json` (via npm install)

**Interfaces:**
- Consumes: nothing.
- Produces: `DonPicasoPreset` export; PrimeNG configured app-wide. All later frontend tasks import PrimeNG modules freely (`primeng/button`, `primeng/inputtext`, `primeng/inputnumber`, `primeng/password`, `primeng/message`, `primeng/dialog`, `primeng/selectbutton`, `primeng/toast`, `primeng/confirmdialog`).

- [ ] **Step 1: Install packages**

```bash
cd frontend
npm install primeng @primeuix/themes primeicons
```

Expected: `primeng` resolves to a 21.x version (matches Angular 21). If npm resolves a lower major, install explicitly with `npm install primeng@21`.

- [ ] **Step 2: Create the theme preset**

`frontend/src/app/app-theme.ts`:

```ts
import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';

/**
 * Aura with an emerald primary — the green/white POS look from the design
 * reference. Kept in its own file so the preset can grow (surface tones,
 * component tokens) without touching app.config.ts.
 */
export const DonPicasoPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '{emerald.50}',
      100: '{emerald.100}',
      200: '{emerald.200}',
      300: '{emerald.300}',
      400: '{emerald.400}',
      500: '{emerald.500}',
      600: '{emerald.600}',
      700: '{emerald.700}',
      800: '{emerald.800}',
      900: '{emerald.900}',
      950: '{emerald.950}',
    },
  },
});
```

- [ ] **Step 3: Register providePrimeNG**

`frontend/src/app/app.config.ts` — add imports and the provider (after `provideHttpClient(...)`):

```ts
import { providePrimeNG } from 'primeng/config';

import { DonPicasoPreset } from './app-theme';
```

```ts
    providePrimeNG({
      theme: {
        preset: DonPicasoPreset,
        // The POS runs on tablets in bright rooms; no dark mode this phase.
        options: { darkModeSelector: false },
      },
    }),
```

- [ ] **Step 4: Add primeicons stylesheet**

`frontend/angular.json` — in the build options `styles` array, before `src/styles.scss`:

```json
              "node_modules/primeicons/primeicons.css",
```

- [ ] **Step 5: Verify build and existing tests**

```bash
cd frontend
npm run build
npm test
```

Expected: build succeeds; all existing Vitest suites still pass.

- [ ] **Step 6: Commit**

```bash
git add frontend/package.json frontend/package-lock.json frontend/angular.json frontend/src/app/app-theme.ts frontend/src/app/app.config.ts
git commit -m "Install PrimeNG with emerald Aura preset"
```

---

### Task 6: Menu models + MenuService with offline cache (TDD)

**Files:**
- Create: `frontend/src/app/core/menu/menu.models.ts`
- Create: `frontend/src/app/core/menu/menu.service.ts`
- Test: `frontend/src/app/core/menu/menu.service.spec.ts`

**Interfaces:**
- Consumes: `GET /api/v1/menu` JSON (Task 2's `MenuResult`, camelCased).
- Produces: `MenuProduct { id: string; name: string; price: number; imageUrl: string | null }`, `MenuCategory { id: string; name: string; products: MenuProduct[] }`, `MenuResponse { categories: MenuCategory[]; taxRatePercent: number }`, `MenuSource = 'network' | 'cache' | 'unavailable'`; `MenuService` (root-provided) with `loadMenu(): Promise<void>` and signals `categories`, `taxRatePercent`, `source`.

- [ ] **Step 1: Create the models**

`frontend/src/app/core/menu/menu.models.ts`:

```ts
/** Mirrors the backend GetMenu contract (GET /api/v1/menu — Modules.Menu). */
export interface MenuProduct {
  id: string;
  name: string;
  price: number;
  imageUrl: string | null;
}

export interface MenuCategory {
  id: string;
  name: string;
  products: MenuProduct[];
}

export interface MenuResponse {
  categories: MenuCategory[];
  taxRatePercent: number;
}
```

- [ ] **Step 2: Write the failing tests**

`frontend/src/app/core/menu/menu.service.spec.ts`:

```ts
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { MenuResponse } from './menu.models';
import { MENU_CACHE_KEY, MenuService } from './menu.service';

const sampleMenu: MenuResponse = {
  categories: [
    {
      id: 'cat-1',
      name: 'Coffee',
      products: [{ id: 'prod-1', name: 'Espresso', price: 2.5, imageUrl: null }],
    },
  ],
  taxRatePercent: 1.5,
};

describe('MenuService', () => {
  let service: MenuService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(MenuService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('loads the menu from the network and caches it', async () => {
    const loadPromise = service.loadMenu();
    httpMock.expectOne('/api/v1/menu').flush(sampleMenu);
    await loadPromise;

    expect(service.source()).toBe('network');
    expect(service.taxRatePercent()).toBe(1.5);
    expect(service.categories()).toEqual(sampleMenu.categories);
    expect(JSON.parse(localStorage.getItem(MENU_CACHE_KEY)!)).toEqual(sampleMenu);
  });

  it('falls back to the cached menu when the network fails', async () => {
    localStorage.setItem(MENU_CACHE_KEY, JSON.stringify(sampleMenu));

    const loadPromise = service.loadMenu();
    httpMock.expectOne('/api/v1/menu').error(new ProgressEvent('offline'));
    await loadPromise;

    expect(service.source()).toBe('cache');
    expect(service.categories()).toEqual(sampleMenu.categories);
    expect(service.taxRatePercent()).toBe(1.5);
  });

  it('reports unavailable when there is no network and no cache', async () => {
    const loadPromise = service.loadMenu();
    httpMock.expectOne('/api/v1/menu').error(new ProgressEvent('offline'));
    await loadPromise;

    expect(service.source()).toBe('unavailable');
    expect(service.categories()).toEqual([]);
  });
});
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `cd frontend && npm test`
Expected: FAIL — cannot resolve `./menu.service`.

- [ ] **Step 4: Implement the service**

`frontend/src/app/core/menu/menu.service.ts`:

```ts
import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { MenuCategory, MenuResponse } from './menu.models';

const MENU_URL = '/api/v1/menu';
export const MENU_CACHE_KEY = 'donpicaso.menuCache';

export type MenuSource = 'network' | 'cache' | 'unavailable';

/**
 * Menu read model for the POS. The last successful fetch is cached in
 * localStorage so a tablet that goes offline can keep taking orders
 * (submission already queues through OrderSyncService).
 */
@Injectable({ providedIn: 'root' })
export class MenuService {
  private readonly http = inject(HttpClient);

  readonly categories = signal<MenuCategory[]>([]);
  readonly taxRatePercent = signal(0);
  readonly source = signal<MenuSource>('unavailable');

  async loadMenu(): Promise<void> {
    try {
      const menu = await firstValueFrom(this.http.get<MenuResponse>(MENU_URL));
      localStorage.setItem(MENU_CACHE_KEY, JSON.stringify(menu));
      this.apply(menu, 'network');
    } catch {
      const cached = localStorage.getItem(MENU_CACHE_KEY);
      if (cached) {
        this.apply(JSON.parse(cached) as MenuResponse, 'cache');
      } else {
        this.source.set('unavailable');
      }
    }
  }

  private apply(menu: MenuResponse, source: MenuSource): void {
    this.categories.set(menu.categories);
    this.taxRatePercent.set(menu.taxRatePercent);
    this.source.set(source);
  }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd frontend && npm test`
Expected: PASS (all suites).

- [ ] **Step 6: Commit**

```bash
git add frontend/src/app/core/menu
git commit -m "Add MenuService with localStorage offline fallback"
```

---

### Task 7: CartService — signal-based money math (TDD)

**Files:**
- Create: `frontend/src/app/features/pos/cart.service.ts`
- Test: `frontend/src/app/features/pos/cart.service.spec.ts`

**Interfaces:**
- Consumes: `MenuService.taxRatePercent` signal (Task 6), `MenuProduct` (Task 6).
- Produces: `roundMoney(value: number): number` (exported function); `CartService` (NOT root-provided — Task 11's `PosShell` provides it) with `CartLine { product: MenuProduct; quantity: number }`, signals `lines`, `discountPercent`, computed `subtotal`, `discountAmount`, `taxAmount`, `total`, and methods `add(product: MenuProduct)`, `increment(productId: string)`, `decrement(productId: string)`, `remove(productId: string)`, `setDiscountPercent(value: number)`, `clear()`.

- [ ] **Step 1: Write the failing tests**

`frontend/src/app/features/pos/cart.service.spec.ts`:

```ts
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { MenuProduct } from '../../core/menu/menu.models';
import { MenuService } from '../../core/menu/menu.service';
import { CartService, roundMoney } from './cart.service';

const espresso: MenuProduct = { id: 'p-1', name: 'Espresso', price: 7.99, imageUrl: null };
const latte: MenuProduct = { id: 'p-2', name: 'Caffe Latte', price: 5.54, imageUrl: null };

describe('roundMoney', () => {
  it('rounds half-up to 2 decimals', () => {
    expect(roundMoney(4.304)).toBe(4.3);
    expect(roundMoney(0.015)).toBe(0.02);
    expect(roundMoney(1.235)).toBe(1.24);
    expect(roundMoney(23.970000000000002)).toBe(23.97);
  });
});

describe('CartService', () => {
  let cart: CartService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CartService, provideHttpClient(), provideHttpClientTesting()],
    });
    TestBed.inject(MenuService).taxRatePercent.set(1.5);
    cart = TestBed.inject(CartService);
  });

  it('adds products and merges repeat adds into one line', () => {
    cart.add(espresso);
    cart.add(espresso);
    cart.add(latte);

    expect(cart.lines()).toEqual([
      { product: espresso, quantity: 2 },
      { product: latte, quantity: 1 },
    ]);
  });

  it('computes subtotal, discount, tax, and total with half-up rounding', () => {
    // 2 × 7.99 + 1 × 5.54 = 21.52; 20% discount = 4.304 -> 4.30;
    // 1.5% tax on 17.22 = 0.2583 -> 0.26; total 17.48.
    cart.add(espresso);
    cart.add(espresso);
    cart.add(latte);
    cart.setDiscountPercent(20);

    expect(cart.subtotal()).toBe(21.52);
    expect(cart.discountAmount()).toBe(4.3);
    expect(cart.taxAmount()).toBe(0.26);
    expect(cart.total()).toBe(17.48);
  });

  it('increments, decrements, and removes a line when quantity hits zero', () => {
    cart.add(espresso);
    cart.increment('p-1');
    expect(cart.lines()[0].quantity).toBe(2);

    cart.decrement('p-1');
    cart.decrement('p-1');
    expect(cart.lines()).toEqual([]);
  });

  it('removes a line directly', () => {
    cart.add(espresso);
    cart.add(latte);
    cart.remove('p-1');

    expect(cart.lines().map((l) => l.product.id)).toEqual(['p-2']);
  });

  it('clamps discount percent to 0..100', () => {
    cart.setDiscountPercent(150);
    expect(cart.discountPercent()).toBe(100);

    cart.setDiscountPercent(-5);
    expect(cart.discountPercent()).toBe(0);
  });

  it('clear resets lines and discount', () => {
    cart.add(espresso);
    cart.setDiscountPercent(10);

    cart.clear();

    expect(cart.lines()).toEqual([]);
    expect(cart.discountPercent()).toBe(0);
    expect(cart.total()).toBe(0);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npm test`
Expected: FAIL — cannot resolve `./cart.service`.

- [ ] **Step 3: Implement the service**

`frontend/src/app/features/pos/cart.service.ts`:

```ts
import { Injectable, computed, inject, signal } from '@angular/core';

import { MenuProduct } from '../../core/menu/menu.models';
import { MenuService } from '../../core/menu/menu.service';

export interface CartLine {
  product: MenuProduct;
  quantity: number;
}

/**
 * Half-up to 2 decimals — must stay in lockstep with RoundMoney() in
 * CreateOrderCommandValidator, which re-derives and rejects drifted math.
 */
export function roundMoney(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

/**
 * The active order being built. Provided by PosShell (not root) so a fresh
 * cart exists per POS session and tests get isolated instances.
 */
@Injectable()
export class CartService {
  private readonly menu = inject(MenuService);

  readonly lines = signal<CartLine[]>([]);
  readonly discountPercent = signal(0);

  readonly subtotal = computed(() =>
    roundMoney(this.lines().reduce((sum, line) => sum + line.quantity * line.product.price, 0)),
  );

  readonly discountAmount = computed(() =>
    roundMoney((this.subtotal() * this.discountPercent()) / 100),
  );

  readonly taxAmount = computed(() =>
    roundMoney(((this.subtotal() - this.discountAmount()) * this.menu.taxRatePercent()) / 100),
  );

  readonly total = computed(() =>
    roundMoney(this.subtotal() - this.discountAmount() + this.taxAmount()),
  );

  add(product: MenuProduct): void {
    const existing = this.lines().find((line) => line.product.id === product.id);
    if (existing) {
      this.increment(product.id);
      return;
    }
    this.lines.update((lines) => [...lines, { product, quantity: 1 }]);
  }

  increment(productId: string): void {
    this.lines.update((lines) =>
      lines.map((line) =>
        line.product.id === productId ? { ...line, quantity: line.quantity + 1 } : line,
      ),
    );
  }

  decrement(productId: string): void {
    this.lines.update((lines) =>
      lines
        .map((line) =>
          line.product.id === productId ? { ...line, quantity: line.quantity - 1 } : line,
        )
        .filter((line) => line.quantity > 0),
    );
  }

  remove(productId: string): void {
    this.lines.update((lines) => lines.filter((line) => line.product.id !== productId));
  }

  setDiscountPercent(value: number): void {
    this.discountPercent.set(Math.min(100, Math.max(0, value)));
  }

  clear(): void {
    this.lines.set([]);
    this.discountPercent.set(0);
  }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd frontend && npm test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/features/pos
git commit -m "Add CartService with discount/tax money math"
```

---

### Task 8: ProductCatalog component (search, grid, category tabs)

**Files:**
- Create: `frontend/src/app/features/pos/product-catalog/product-catalog.ts`
- Create: `frontend/src/app/features/pos/product-catalog/product-catalog.html`
- Create: `frontend/src/app/features/pos/product-catalog/product-catalog.scss`
- Test: `frontend/src/app/features/pos/product-catalog/product-catalog.spec.ts`

**Interfaces:**
- Consumes: `MenuService.categories` (Task 6), `CartService.add` (Task 7).
- Produces: `<app-product-catalog />` — no inputs/outputs; it talks to the two services directly. Task 11 places it in the shell.

- [ ] **Step 1: Write the failing tests**

`frontend/src/app/features/pos/product-catalog/product-catalog.spec.ts`:

```ts
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { MenuCategory } from '../../../core/menu/menu.models';
import { MenuService } from '../../../core/menu/menu.service';
import { CartService } from '../cart.service';
import { ProductCatalog } from './product-catalog';

const categories: MenuCategory[] = [
  {
    id: 'cat-coffee',
    name: 'Coffee',
    products: [
      { id: 'p-espresso', name: 'Espresso', price: 2.5, imageUrl: null },
      { id: 'p-latte', name: 'Caffe Latte', price: 4.25, imageUrl: null },
    ],
  },
  {
    id: 'cat-snacks',
    name: 'Snacks',
    products: [{ id: 'p-fries', name: 'French Fries', price: 3.25, imageUrl: null }],
  },
];

describe('ProductCatalog', () => {
  let cart: CartService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductCatalog],
      providers: [CartService, provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    TestBed.inject(MenuService).categories.set(categories);
    cart = TestBed.inject(CartService);
  });

  it('renders the first category by default with one tab per category', () => {
    const fixture = TestBed.createComponent(ProductCatalog);
    fixture.detectChanges();

    const tiles = fixture.nativeElement.querySelectorAll('.product-tile');
    const tabs = fixture.nativeElement.querySelectorAll('.category-tab');

    expect(tabs.length).toBe(2);
    expect(tiles.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('Espresso');
  });

  it('switches products when a category tab is clicked', () => {
    const fixture = TestBed.createComponent(ProductCatalog);
    fixture.detectChanges();

    const snacksTab = Array.from(
      fixture.nativeElement.querySelectorAll<HTMLButtonElement>('.category-tab'),
    ).find((tab) => tab.textContent!.includes('Snacks'))!;
    snacksTab.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('French Fries');
    expect(fixture.nativeElement.textContent).not.toContain('Espresso');
  });

  it('filters products by the search term within the active category', () => {
    const fixture = TestBed.createComponent(ProductCatalog);
    fixture.detectChanges();

    fixture.componentInstance['searchTerm'].set('latte');
    fixture.detectChanges();

    const tiles = fixture.nativeElement.querySelectorAll('.product-tile');
    expect(tiles.length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Caffe Latte');
  });

  it('adds the product to the cart when a tile is tapped', () => {
    const fixture = TestBed.createComponent(ProductCatalog);
    fixture.detectChanges();

    fixture.nativeElement.querySelector<HTMLButtonElement>('.product-tile')!.click();

    expect(cart.lines()).toEqual([
      { product: categories[0].products[0], quantity: 1 },
    ]);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npm test`
Expected: FAIL — cannot resolve `./product-catalog`.

- [ ] **Step 3: Implement the component**

`frontend/src/app/features/pos/product-catalog/product-catalog.ts`:

```ts
import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';

import { MenuService } from '../../../core/menu/menu.service';
import { CartService } from '../cart.service';

@Component({
  selector: 'app-product-catalog',
  imports: [CurrencyPipe, FormsModule, InputTextModule],
  templateUrl: './product-catalog.html',
  styleUrl: './product-catalog.scss',
})
export class ProductCatalog {
  protected readonly menu = inject(MenuService);
  protected readonly cart = inject(CartService);

  protected readonly searchTerm = signal('');
  protected readonly selectedCategoryId = signal<string | null>(null);

  protected readonly selectedCategory = computed(() => {
    const categories = this.menu.categories();
    return categories.find((c) => c.id === this.selectedCategoryId()) ?? categories[0] ?? null;
  });

  protected readonly filteredProducts = computed(() => {
    const category = this.selectedCategory();
    if (!category) {
      return [];
    }
    const term = this.searchTerm().trim().toLowerCase();
    return term
      ? category.products.filter((p) => p.name.toLowerCase().includes(term))
      : category.products;
  });

  /** Placeholder tile art until real product images exist (deferred). */
  protected initials(name: string): string {
    return name
      .split(' ')
      .slice(0, 2)
      .map((word) => word[0])
      .join('')
      .toUpperCase();
  }
}
```

`frontend/src/app/features/pos/product-catalog/product-catalog.html`:

```html
<div class="catalog">
  <div class="search-bar">
    <i class="pi pi-search"></i>
    <input
      pInputText
      type="text"
      name="search"
      placeholder="Search items here..."
      [ngModel]="searchTerm()"
      (ngModelChange)="searchTerm.set($event)"
    />
  </div>

  <div class="product-grid">
    @for (product of filteredProducts(); track product.id) {
      <button type="button" class="product-tile" (click)="cart.add(product)">
        @if (product.imageUrl) {
          <img [src]="product.imageUrl" [alt]="product.name" />
        } @else {
          <span class="tile-placeholder">{{ initials(product.name) }}</span>
        }
        <span class="tile-name">{{ product.name }}</span>
        <span class="tile-price">{{ product.price | currency }}</span>
      </button>
    } @empty {
      <p class="no-results">No items match your search.</p>
    }
  </div>

  <nav class="category-tabs">
    @for (category of menu.categories(); track category.id) {
      <button
        type="button"
        class="category-tab"
        [class.active]="category.id === selectedCategory()?.id"
        (click)="selectedCategoryId.set(category.id); searchTerm.set('')"
      >
        <i class="pi pi-tag"></i>
        <span>{{ category.name }}</span>
      </button>
    }
  </nav>
</div>
```

`frontend/src/app/features/pos/product-catalog/product-catalog.scss`:

```scss
.catalog {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  height: 100%;
  min-height: 0;
}

.search-bar {
  display: flex;
  align-items: center;
  gap: 0.5rem;

  i {
    color: var(--p-primary-600);
  }

  input {
    flex: 1;
    max-width: 320px;
  }
}

.product-grid {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
  gap: 1rem;
  align-content: start;
}

.product-tile {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  padding: 1rem;
  background: #fff;
  border: 1px solid var(--p-surface-200);
  border-radius: 8px;
  cursor: pointer;
  transition: border-color 0.15s;

  &:hover {
    border-color: var(--p-primary-500);
  }

  img,
  .tile-placeholder {
    width: 80px;
    height: 80px;
    border-radius: 8px;
    object-fit: cover;
  }

  .tile-placeholder {
    display: grid;
    place-items: center;
    background: var(--p-primary-50);
    color: var(--p-primary-700);
    font-size: 1.5rem;
    font-weight: 700;
  }

  .tile-name {
    font-weight: 600;
    text-align: center;
  }

  .tile-price {
    color: var(--p-primary-600);
    font-weight: 700;
  }
}

.no-results {
  color: var(--p-surface-500);
}

.category-tabs {
  display: flex;
  gap: 0.75rem;
  overflow-x: auto;
  padding-bottom: 0.25rem;

  .category-tab {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.25rem;
    min-width: 96px;
    padding: 0.75rem 1rem;
    background: #fff;
    border: 1px solid var(--p-surface-200);
    border-radius: 8px;
    color: var(--p-surface-500);
    cursor: pointer;

    &.active {
      border-color: var(--p-primary-600);
      color: var(--p-primary-700);
      font-weight: 600;
    }
  }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd frontend && npm test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/features/pos/product-catalog
git commit -m "Add POS product catalog with search and category tabs"
```

---

### Task 9: CartPanel component (lines, steppers, discount, totals)

**Files:**
- Create: `frontend/src/app/features/pos/cart-panel/cart-panel.ts`
- Create: `frontend/src/app/features/pos/cart-panel/cart-panel.html`
- Create: `frontend/src/app/features/pos/cart-panel/cart-panel.scss`
- Test: `frontend/src/app/features/pos/cart-panel/cart-panel.spec.ts`

**Interfaces:**
- Consumes: `CartService` (Task 7), PrimeNG `ConfirmationService` (host dialog lives in the shell, Task 11).
- Produces: `<app-cart-panel (pay)="..." />` — single `pay` output emitted when the Pay button is pressed with a non-empty cart.

- [ ] **Step 1: Write the failing tests**

`frontend/src/app/features/pos/cart-panel/cart-panel.spec.ts`:

```ts
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Confirmation, ConfirmationService } from 'primeng/api';

import { MenuService } from '../../../core/menu/menu.service';
import { CartService } from '../cart.service';
import { CartPanel } from './cart-panel';

const espresso = { id: 'p-1', name: 'Espresso', price: 2.5, imageUrl: null };

describe('CartPanel', () => {
  let cart: CartService;
  let confirmationService: ConfirmationService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CartPanel],
      providers: [
        CartService,
        ConfirmationService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    TestBed.inject(MenuService).taxRatePercent.set(1.5);
    cart = TestBed.inject(CartService);
    confirmationService = TestBed.inject(ConfirmationService);
  });

  it('renders cart lines with quantities and totals', () => {
    cart.add(espresso);
    cart.add(espresso);

    const fixture = TestBed.createComponent(CartPanel);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Espresso');
    expect(fixture.nativeElement.querySelector('.line-quantity')!.textContent).toContain('2');
    expect(fixture.nativeElement.textContent).toContain('$5.00');
  });

  it('disables Pay when the cart is empty and emits pay when pressed with items', () => {
    const fixture = TestBed.createComponent(CartPanel);
    fixture.detectChanges();

    const payButton = fixture.nativeElement.querySelector<HTMLButtonElement>('.pay-button button');
    expect(payButton!.disabled).toBe(true);

    cart.add(espresso);
    fixture.detectChanges();

    let emitted = false;
    fixture.componentInstance.pay.subscribe(() => (emitted = true));
    payButton!.click();

    expect(emitted).toBe(true);
  });

  it('cancel order asks for confirmation and clears the cart on accept', () => {
    cart.add(espresso);
    const fixture = TestBed.createComponent(CartPanel);
    fixture.detectChanges();

    let captured: Confirmation | undefined;
    vi.spyOn(confirmationService, 'confirm').mockImplementation((confirmation: Confirmation) => {
      captured = confirmation;
      return confirmationService;
    });

    fixture.nativeElement.querySelector<HTMLButtonElement>('.cancel-button button')!.click();
    expect(captured).toBeDefined();

    captured!.accept!();
    expect(cart.lines()).toEqual([]);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npm test`
Expected: FAIL — cannot resolve `./cart-panel`.

- [ ] **Step 3: Implement the component**

`frontend/src/app/features/pos/cart-panel/cart-panel.ts`:

```ts
import { CurrencyPipe } from '@angular/common';
import { Component, inject, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';

import { CartService } from '../cart.service';

@Component({
  selector: 'app-cart-panel',
  imports: [CurrencyPipe, FormsModule, ButtonModule, InputNumberModule],
  templateUrl: './cart-panel.html',
  styleUrl: './cart-panel.scss',
})
export class CartPanel {
  protected readonly cart = inject(CartService);
  private readonly confirmationService = inject(ConfirmationService);

  readonly pay = output<void>();

  protected cancelOrder(): void {
    this.confirmationService.confirm({
      header: 'Cancel Order',
      message: 'Clear all items from this order?',
      icon: 'pi pi-exclamation-triangle',
      accept: () => this.cart.clear(),
    });
  }
}
```

`frontend/src/app/features/pos/cart-panel/cart-panel.html`:

```html
<aside class="cart-panel">
  <h2>Checkout</h2>

  <div class="cart-lines">
    @for (line of cart.lines(); track line.product.id) {
      <div class="cart-line">
        <p-button
          icon="pi pi-trash"
          [text]="true"
          severity="danger"
          ariaLabel="Remove item"
          (onClick)="cart.remove(line.product.id)"
        />
        <span class="line-name">{{ line.product.name }}</span>
        <span class="line-stepper">
          <p-button
            icon="pi pi-minus"
            [rounded]="true"
            [outlined]="true"
            ariaLabel="Decrease quantity"
            (onClick)="cart.decrement(line.product.id)"
          />
          <span class="line-quantity">{{ line.quantity }}</span>
          <p-button
            icon="pi pi-plus"
            [rounded]="true"
            [outlined]="true"
            ariaLabel="Increase quantity"
            (onClick)="cart.increment(line.product.id)"
          />
        </span>
        <span class="line-total">{{ line.quantity * line.product.price | currency }}</span>
      </div>
    } @empty {
      <p class="empty-cart">Tap items to add them to the order.</p>
    }
  </div>

  <div class="cart-summary">
    <div class="summary-row">
      <label for="discount">Discount (%)</label>
      <p-inputNumber
        inputId="discount"
        name="discount"
        [min]="0"
        [max]="100"
        [ngModel]="cart.discountPercent()"
        (ngModelChange)="cart.setDiscountPercent($event ?? 0)"
      />
    </div>
    <div class="summary-row">
      <span>Sub Total</span>
      <span>{{ cart.subtotal() | currency }}</span>
    </div>
    <div class="summary-row">
      <span>Discount</span>
      <span>-{{ cart.discountAmount() | currency }}</span>
    </div>
    <div class="summary-row">
      <span>Tax</span>
      <span>{{ cart.taxAmount() | currency }}</span>
    </div>
    <div class="summary-row total-row">
      <span>Total</span>
      <span>{{ cart.total() | currency }}</span>
    </div>
  </div>

  <div class="cart-actions">
    <p-button
      class="cancel-button"
      label="Cancel Order"
      severity="danger"
      [outlined]="true"
      [disabled]="cart.lines().length === 0"
      (onClick)="cancelOrder()"
    />
    <p-button class="pay-button" [disabled]="cart.lines().length === 0" (onClick)="pay.emit()">
      Pay ({{ cart.total() | currency }})
    </p-button>
  </div>
</aside>
```

`frontend/src/app/features/pos/cart-panel/cart-panel.scss`:

```scss
.cart-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  background: #fff;
  border: 1px solid var(--p-surface-200);
  border-radius: 8px;
  padding: 1rem;

  h2 {
    margin: 0 0 1rem;
    text-align: center;
    font-size: 1.25rem;
  }
}

.cart-lines {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
}

.cart-line {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0;
  border-bottom: 1px solid var(--p-surface-100);

  .line-name {
    flex: 1;
    font-weight: 600;
  }

  .line-stepper {
    display: flex;
    align-items: center;
    gap: 0.25rem;
  }

  .line-total {
    min-width: 64px;
    text-align: right;
    font-weight: 600;
  }
}

.empty-cart {
  color: var(--p-surface-500);
  text-align: center;
  margin-top: 2rem;
}

.cart-summary {
  border-top: 1px solid var(--p-surface-200);
  padding-top: 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;

  .summary-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .total-row {
    font-size: 1.25rem;
    font-weight: 700;

    span:last-child {
      color: var(--p-primary-600);
    }
  }
}

.cart-actions {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-top: 1rem;

  .pay-button,
  .cancel-button {
    display: block;

    ::ng-deep button {
      width: 100%;
      justify-content: center;
    }
  }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd frontend && npm test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/features/pos/cart-panel
git commit -m "Add POS cart panel with steppers, discount, and totals"
```

---

### Task 10: PaymentDialog + extended order payload

**Files:**
- Modify: `frontend/src/app/core/offline/offline-order-db.ts` (extend payload types)
- Create: `frontend/src/app/features/pos/payment-dialog/payment-dialog.ts`
- Create: `frontend/src/app/features/pos/payment-dialog/payment-dialog.html`
- Create: `frontend/src/app/features/pos/payment-dialog/payment-dialog.scss`
- Test: `frontend/src/app/features/pos/payment-dialog/payment-dialog.spec.ts`

**Interfaces:**
- Consumes: `CartService` (Task 7), `MenuService.taxRatePercent` (Task 6), `AuthService.currentUser`, `OrderSyncService.placeOrder(order: NewOrder)`, PrimeNG `MessageService` (toast host lives in the shell, Task 11).
- Produces: extended `CreateOrderPayload`/`NewOrder` with `subtotal`, `discountPercent`, `discountAmount`, `taxRatePercent`, `taxAmount`, `paymentMethod: PaymentMethod`, `cashTendered: number | null`, `changeDue: number | null` (matches Task 4's backend command, camelCased; enum as string); `<app-payment-dialog [(visible)]="..." />` with a `visible` model signal.

- [ ] **Step 1: Extend the offline payload types**

`frontend/src/app/core/offline/offline-order-db.ts` — add the payment method type above `CreateOrderPayload` and replace the `CreateOrderPayload` interface (Dexie schema is untouched: the payload is stored opaquely and never indexed, so no version bump):

```ts
export type PaymentMethod = 'Cash' | 'Card';
```

```ts
export interface CreateOrderPayload {
  /**
   * Device-generated idempotency key (UUID). Assigned once when the order is
   * placed and reused verbatim on every replay, so the backend can detect an
   * order it already received even if the original response was lost.
   */
  clientOrderId: string;
  branchId: string;
  brandId: string;
  subtotal: number;
  discountPercent: number;
  discountAmount: number;
  taxRatePercent: number;
  taxAmount: number;
  totalAmount: number;
  paymentMethod: PaymentMethod;
  cashTendered: number | null;
  changeDue: number | null;
  items: OrderItemPayload[];
}
```

- [ ] **Step 2: Write the failing tests**

`frontend/src/app/features/pos/payment-dialog/payment-dialog.spec.ts`:

```ts
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MessageService } from 'primeng/api';

import { AuthService } from '../../../core/auth/auth.service';
import { Role } from '../../../core/auth/auth.models';
import { MenuService } from '../../../core/menu/menu.service';
import { NewOrder } from '../../../core/offline/offline-order-db';
import { OrderSyncService } from '../../../core/offline/order-sync.service';
import { CartService } from '../cart.service';
import { PaymentDialog } from './payment-dialog';

const espresso = { id: 'p-1', name: 'Espresso', price: 7.99, imageUrl: null };
const latte = { id: 'p-2', name: 'Caffe Latte', price: 5.54, imageUrl: null };

describe('PaymentDialog', () => {
  let cart: CartService;
  let placeOrderMock: ReturnType<typeof vi.fn>;
  let messageAddSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(async () => {
    placeOrderMock = vi.fn().mockResolvedValue({ status: 'sent', orderId: 'order-1' });

    await TestBed.configureTestingModule({
      imports: [PaymentDialog],
      providers: [
        CartService,
        MessageService,
        { provide: OrderSyncService, useValue: { placeOrder: placeOrderMock } },
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    TestBed.inject(MenuService).taxRatePercent.set(1.5);
    TestBed.inject(AuthService).currentUser.set({
      userId: 'user-1',
      role: Role.Staff,
      brandId: 'brand-1',
      branchId: 'branch-1',
    });
    messageAddSpy = vi.spyOn(TestBed.inject(MessageService), 'add');

    cart = TestBed.inject(CartService);
    // 2 × 7.99 + 1 × 5.54 = 21.52; 20% discount = 4.30; tax 0.26; total 17.48.
    cart.add(espresso);
    cart.add(espresso);
    cart.add(latte);
    cart.setDiscountPercent(20);
  });

  it('computes change due and blocks confirm while tendered is below the total', () => {
    const fixture = TestBed.createComponent(PaymentDialog);
    const component = fixture.componentInstance;

    component['cashTendered'].set(10);
    expect(component['changeDue']()).toBe(-7.48);
    expect(component['canConfirm']()).toBe(false);

    component['cashTendered'].set(20);
    expect(component['changeDue']()).toBe(2.52);
    expect(component['canConfirm']()).toBe(true);
  });

  it('submits the full cash payload, resets the cart, and closes', async () => {
    const fixture = TestBed.createComponent(PaymentDialog);
    const component = fixture.componentInstance;
    component.visible.set(true);
    component['cashTendered'].set(20);

    await component['confirm']();

    const payload = placeOrderMock.mock.calls[0][0] as NewOrder;
    expect(payload).toEqual({
      branchId: 'branch-1',
      brandId: 'brand-1',
      subtotal: 21.52,
      discountPercent: 20,
      discountAmount: 4.3,
      taxRatePercent: 1.5,
      taxAmount: 0.26,
      totalAmount: 17.48,
      paymentMethod: 'Cash',
      cashTendered: 20,
      changeDue: 2.52,
      items: [
        { productId: 'p-1', productName: 'Espresso', quantity: 2, unitPrice: 7.99 },
        { productId: 'p-2', productName: 'Caffe Latte', quantity: 1, unitPrice: 5.54 },
      ],
    });
    expect(cart.lines()).toEqual([]);
    expect(component.visible()).toBe(false);
    expect(messageAddSpy).toHaveBeenCalledWith(
      expect.objectContaining({ severity: 'success' }),
    );
  });

  it('submits card payments with null cash fields and no tendered requirement', async () => {
    const fixture = TestBed.createComponent(PaymentDialog);
    const component = fixture.componentInstance;
    component.visible.set(true);
    component['method'].set('Card');

    expect(component['canConfirm']()).toBe(true);

    await component['confirm']();

    const payload = placeOrderMock.mock.calls[0][0] as NewOrder;
    expect(payload.paymentMethod).toBe('Card');
    expect(payload.cashTendered).toBeNull();
    expect(payload.changeDue).toBeNull();
  });

  it('warns instead of celebrating when the order was queued offline', async () => {
    placeOrderMock.mockResolvedValue({ status: 'queued-offline' });
    const fixture = TestBed.createComponent(PaymentDialog);
    const component = fixture.componentInstance;
    component.visible.set(true);
    component['method'].set('Card');

    await component['confirm']();

    expect(messageAddSpy).toHaveBeenCalledWith(expect.objectContaining({ severity: 'warn' }));
    expect(cart.lines()).toEqual([]);
  });

  it('keeps the cart intact when the backend rejects the order', async () => {
    placeOrderMock.mockRejectedValue(new Error('400'));
    const fixture = TestBed.createComponent(PaymentDialog);
    const component = fixture.componentInstance;
    component.visible.set(true);
    component['method'].set('Card');

    await component['confirm']();

    expect(messageAddSpy).toHaveBeenCalledWith(expect.objectContaining({ severity: 'error' }));
    expect(cart.lines()).toHaveLength(2);
    expect(component.visible()).toBe(true);
  });
});
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `cd frontend && npm test`
Expected: FAIL — cannot resolve `./payment-dialog`.

- [ ] **Step 4: Implement the component**

`frontend/src/app/features/pos/payment-dialog/payment-dialog.ts`:

```ts
import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject, model, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectButtonModule } from 'primeng/selectbutton';

import { AuthService } from '../../../core/auth/auth.service';
import { MenuService } from '../../../core/menu/menu.service';
import { PaymentMethod } from '../../../core/offline/offline-order-db';
import { OrderSyncService } from '../../../core/offline/order-sync.service';
import { CartService, roundMoney } from '../cart.service';

@Component({
  selector: 'app-payment-dialog',
  imports: [CurrencyPipe, FormsModule, ButtonModule, DialogModule, InputNumberModule, SelectButtonModule],
  templateUrl: './payment-dialog.html',
  styleUrl: './payment-dialog.scss',
})
export class PaymentDialog {
  protected readonly cart = inject(CartService);
  private readonly menu = inject(MenuService);
  private readonly authService = inject(AuthService);
  private readonly orderSync = inject(OrderSyncService);
  private readonly messageService = inject(MessageService);

  readonly visible = model(false);

  protected readonly methodOptions: { label: string; value: PaymentMethod }[] = [
    { label: 'Cash', value: 'Cash' },
    { label: 'Card', value: 'Card' },
  ];

  protected readonly method = signal<PaymentMethod>('Cash');
  protected readonly cashTendered = signal<number | null>(null);
  protected readonly isSubmitting = signal(false);

  protected readonly changeDue = computed(() => {
    const tendered = this.cashTendered();
    return tendered === null ? null : roundMoney(tendered - this.cart.total());
  });

  protected readonly canConfirm = computed(
    () => this.method() === 'Card' || (this.changeDue() !== null && this.changeDue()! >= 0),
  );

  protected async confirm(): Promise<void> {
    const user = this.authService.currentUser();
    if (!user?.branchId || !user.brandId || !this.canConfirm() || this.isSubmitting()) {
      return;
    }

    this.isSubmitting.set(true);
    try {
      const isCash = this.method() === 'Cash';
      const result = await this.orderSync.placeOrder({
        branchId: user.branchId,
        brandId: user.brandId,
        subtotal: this.cart.subtotal(),
        discountPercent: this.cart.discountPercent(),
        discountAmount: this.cart.discountAmount(),
        taxRatePercent: this.menu.taxRatePercent(),
        taxAmount: this.cart.taxAmount(),
        totalAmount: this.cart.total(),
        paymentMethod: this.method(),
        cashTendered: isCash ? this.cashTendered() : null,
        changeDue: isCash ? this.changeDue() : null,
        items: this.cart.lines().map((line) => ({
          productId: line.product.id,
          productName: line.product.name,
          quantity: line.quantity,
          unitPrice: line.product.price,
        })),
      });

      this.messageService.add(
        result.status === 'sent'
          ? { severity: 'success', summary: 'Order placed' }
          : { severity: 'warn', summary: 'Order queued — offline', detail: 'It will sync when the connection returns.' },
      );
      this.cart.clear();
      this.reset();
      this.visible.set(false);
    } catch {
      // 4xx/5xx from the backend — the money math should make this
      // impossible from a correct client, so treat it as a bug surface:
      // keep the cart so nothing is lost.
      this.messageService.add({
        severity: 'error',
        summary: "Couldn't place order",
        detail: 'The order was kept — try again.',
      });
    } finally {
      this.isSubmitting.set(false);
    }
  }

  protected close(): void {
    this.reset();
    this.visible.set(false);
  }

  private reset(): void {
    this.method.set('Cash');
    this.cashTendered.set(null);
  }
}
```

`frontend/src/app/features/pos/payment-dialog/payment-dialog.html`:

```html
<p-dialog
  header="Payment"
  [modal]="true"
  [visible]="visible()"
  (visibleChange)="visible.set($event)"
  [style]="{ width: '24rem' }"
  (onHide)="close()"
>
  <div class="payment-body">
    <div class="amount-due">
      <span>Amount due</span>
      <strong>{{ cart.total() | currency }}</strong>
    </div>

    <p-selectbutton
      name="method"
      [options]="methodOptions"
      optionLabel="label"
      optionValue="value"
      [allowEmpty]="false"
      [ngModel]="method()"
      (ngModelChange)="method.set($event)"
    />

    @if (method() === 'Cash') {
      <div class="cash-fields">
        <label for="tendered">Cash tendered</label>
        <p-inputNumber
          inputId="tendered"
          name="tendered"
          mode="currency"
          currency="USD"
          [min]="0"
          [ngModel]="cashTendered()"
          (ngModelChange)="cashTendered.set($event)"
        />
        @if (changeDue() !== null) {
          <div class="change-due" [class.negative]="changeDue()! < 0">
            <span>Change due</span>
            <strong>{{ changeDue() | currency }}</strong>
          </div>
        }
      </div>
    }
  </div>

  <ng-template #footer>
    <p-button label="Back" [text]="true" (onClick)="close()" />
    <p-button
      class="confirm-button"
      label="Confirm payment"
      [disabled]="!canConfirm() || isSubmitting()"
      [loading]="isSubmitting()"
      (onClick)="confirm()"
    />
  </ng-template>
</p-dialog>
```

`frontend/src/app/features/pos/payment-dialog/payment-dialog.scss`:

```scss
.payment-body {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.amount-due {
  display: flex;
  justify-content: space-between;
  align-items: baseline;

  strong {
    font-size: 1.5rem;
    color: var(--p-primary-600);
  }
}

.cash-fields {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.change-due {
  display: flex;
  justify-content: space-between;

  &.negative strong {
    color: var(--p-red-500);
  }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd frontend && npm test`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/app/core/offline/offline-order-db.ts frontend/src/app/features/pos/payment-dialog
git commit -m "Add payment dialog and extend order payload with payment fields"
```

---

### Task 11: PosShell — assemble the screen, logout, offline indicator, route swap

**Files:**
- Create: `frontend/src/app/features/pos/pos-shell/pos-shell.ts`
- Create: `frontend/src/app/features/pos/pos-shell/pos-shell.html`
- Create: `frontend/src/app/features/pos/pos-shell/pos-shell.scss`
- Test: `frontend/src/app/features/pos/pos-shell/pos-shell.spec.ts`
- Modify: `frontend/src/app/app.routes.ts` (swap placeholder for PosShell)
- Delete: `frontend/src/app/features/pos/pos-placeholder.ts`

**Interfaces:**
- Consumes: `ProductCatalog` (Task 8), `CartPanel` (Task 9), `PaymentDialog` (Task 10), `MenuService`, `CartService`, `OrderSyncService`, `AuthService.logout()`.
- Produces: `PosShell` routed at `/pos`; provides `CartService`, `MessageService`, `ConfirmationService` and hosts `<p-toast />` + `<p-confirmdialog />` for the whole POS subtree.

- [ ] **Step 1: Write the failing tests**

`frontend/src/app/features/pos/pos-shell/pos-shell.spec.ts`:

```ts
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { MenuResponse } from '../../../core/menu/menu.models';
import { OrderSyncService } from '../../../core/offline/order-sync.service';
import { PosShell } from './pos-shell';

const sampleMenu: MenuResponse = {
  categories: [
    {
      id: 'cat-1',
      name: 'Coffee',
      products: [{ id: 'p-1', name: 'Espresso', price: 2.5, imageUrl: null }],
    },
  ],
  taxRatePercent: 1.5,
};

describe('PosShell', () => {
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [PosShell],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        // Stub the root OrderSyncService: its constructor touches IndexedDB,
        // which jsdom lacks — same pattern as payment-dialog.spec.ts.
        { provide: OrderSyncService, useValue: { isOnline: signal(true), pendingCount: signal(0) } },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => httpMock.verify());

  it('loads the menu on init and renders catalog + cart', async () => {
    const fixture = TestBed.createComponent(PosShell);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/menu').flush(sampleMenu);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-product-catalog')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('app-cart-panel')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Espresso');
  });

  it('shows the retry state when the menu is unavailable', async () => {
    const fixture = TestBed.createComponent(PosShell);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/menu').error(new ProgressEvent('offline'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.menu-unavailable')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('app-product-catalog')).toBeFalsy();
  });

  it('logs out to the staff PIN screen when the cart is empty', async () => {
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');
    const fixture = TestBed.createComponent(PosShell);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/menu').flush(sampleMenu);
    await fixture.whenStable();

    await fixture.componentInstance['doLogout']();

    // AuthService.logout() only calls the API when a refresh token exists;
    // with clean localStorage it just clears the session locally.
    expect(navigateSpy).toHaveBeenCalledWith('/staff-login');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npm test`
Expected: FAIL — cannot resolve `./pos-shell`.

- [ ] **Step 3: Implement the shell**

`frontend/src/app/features/pos/pos-shell/pos-shell.ts`:

```ts
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';

import { AuthService } from '../../../core/auth/auth.service';
import { MenuService } from '../../../core/menu/menu.service';
import { OrderSyncService } from '../../../core/offline/order-sync.service';
import { CartService } from '../cart.service';
import { CartPanel } from '../cart-panel/cart-panel';
import { PaymentDialog } from '../payment-dialog/payment-dialog';
import { ProductCatalog } from '../product-catalog/product-catalog';

@Component({
  selector: 'app-pos-shell',
  imports: [ButtonModule, ConfirmDialogModule, ToastModule, CartPanel, PaymentDialog, ProductCatalog],
  providers: [CartService, MessageService, ConfirmationService],
  templateUrl: './pos-shell.html',
  styleUrl: './pos-shell.scss',
})
export class PosShell implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly menu = inject(MenuService);
  protected readonly cart = inject(CartService);
  protected readonly orderSync = inject(OrderSyncService);

  protected readonly paymentVisible = signal(false);

  ngOnInit(): void {
    void this.menu.loadMenu();
  }

  protected retry(): void {
    void this.menu.loadMenu();
  }

  protected logout(): void {
    if (this.cart.lines().length > 0) {
      this.confirmationService.confirm({
        header: 'Log out',
        message: 'The current order will be discarded. Log out?',
        icon: 'pi pi-exclamation-triangle',
        accept: () => void this.doLogout(),
      });
      return;
    }
    void this.doLogout();
  }

  private async doLogout(): Promise<void> {
    await this.authService.logout();
    // Device branch binding is untouched — the tablet lands on the PIN
    // screen ready for the next staff member. Fire-and-forget navigation:
    // Angular 21.2 rejects unmatched-route navigations under provideRouter([])
    // in specs (same precedent as staff-login.ts).
    void this.router.navigateByUrl('/staff-login');
  }
}
```

`frontend/src/app/features/pos/pos-shell/pos-shell.html`:

```html
<p-toast position="top-center" />
<p-confirmdialog />

<div class="pos-layout">
  <header class="pos-topbar">
    <span class="brand-name">Don Picaso POS</span>

    @if (!orderSync.isOnline() || orderSync.pendingCount() > 0 || menu.source() === 'cache') {
      <span class="offline-badge">
        <i class="pi pi-wifi"></i>
        Offline
        @if (orderSync.pendingCount() > 0) {
          — {{ orderSync.pendingCount() }} queued
        }
      </span>
    }

    <p-button
      class="logout-button"
      label="Logout"
      icon="pi pi-sign-out"
      [text]="true"
      (onClick)="logout()"
    />
  </header>

  @if (menu.source() === 'unavailable') {
    <div class="menu-unavailable">
      <i class="pi pi-exclamation-circle"></i>
      <p>Couldn't load the menu. Check the connection and try again.</p>
      <p-button label="Retry" icon="pi pi-refresh" (onClick)="retry()" />
    </div>
  } @else {
    <main class="pos-main">
      <app-product-catalog />
      <app-cart-panel (pay)="paymentVisible.set(true)" />
    </main>
  }
</div>

<app-payment-dialog [(visible)]="paymentVisible" />
```

`frontend/src/app/features/pos/pos-shell/pos-shell.scss`:

```scss
.pos-layout {
  display: flex;
  flex-direction: column;
  height: 100vh;
  background: var(--p-surface-100);
}

.pos-topbar {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 0.5rem 1rem;
  background: var(--p-primary-600);
  color: #fff;

  .brand-name {
    font-size: 1.15rem;
    font-weight: 700;
    flex: 1;
  }

  .offline-badge {
    display: flex;
    align-items: center;
    gap: 0.4rem;
    padding: 0.25rem 0.75rem;
    border-radius: 999px;
    background: rgb(0 0 0 / 25%);
    font-size: 0.85rem;
  }

  .logout-button ::ng-deep button {
    color: #fff;
  }
}

.pos-main {
  flex: 1;
  min-height: 0;
  display: grid;
  grid-template-columns: 1fr 360px;
  gap: 1rem;
  padding: 1rem;
}

.menu-unavailable {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  color: var(--p-surface-600);

  i {
    font-size: 3rem;
    color: var(--p-primary-500);
  }
}
```

- [ ] **Step 4: Swap the route and delete the placeholder**

`frontend/src/app/app.routes.ts` — replace the `/pos` route's loadComponent:

```ts
  {
    path: 'pos',
    canActivate: [roleGuard(Role.Staff)],
    loadComponent: () => import('./features/pos/pos-shell/pos-shell').then((m) => m.PosShell),
  },
```

```bash
rm frontend/src/app/features/pos/pos-placeholder.ts
```

- [ ] **Step 5: Run all frontend tests + build**

Run: `cd frontend && npm test && npm run build`
Expected: PASS; build succeeds.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/app/features/pos frontend/src/app/app.routes.ts
git commit -m "Assemble POS shell with logout, offline indicator, and route"
```

---

### Task 12: Login page redesign (PrimeNG)

Behavior is untouched — same fields, same submit, same error signal — so the existing spec keeps passing as-is; one rendering assertion is added.

**Files:**
- Modify: `frontend/src/app/features/auth/login/login.ts` (imports only)
- Modify: `frontend/src/app/features/auth/login/login.html`
- Modify: `frontend/src/app/features/auth/login/login.scss`
- Modify: `frontend/src/app/features/auth/login/login.spec.ts` (add render test)

**Interfaces:**
- Consumes: PrimeNG modules (Task 5). No API changes.
- Produces: nothing new.

- [ ] **Step 1: Add the failing render test**

Append to the `describe` block in `frontend/src/app/features/auth/login/login.spec.ts`:

```ts
  it('renders the PrimeNG login card', () => {
    const fixture = TestBed.createComponent(Login);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.login-card')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('p-password')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.login-card p-button button')).toBeTruthy();
  });
```

Run: `cd frontend && npm test`
Expected: the new test FAILS (no `.login-card` yet); the two existing tests still pass.

- [ ] **Step 2: Update the component imports**

`frontend/src/app/features/auth/login/login.ts` — replace the `@Component` imports array and add the module imports (class body unchanged):

```ts
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';
```

```ts
  imports: [FormsModule, ButtonModule, InputTextModule, MessageModule, PasswordModule],
```

- [ ] **Step 3: Rewrite the template and styles**

`frontend/src/app/features/auth/login/login.html`:

```html
<div class="login-page">
  <form class="login-card" (ngSubmit)="submit()">
    <div class="brand-mark">
      <i class="pi pi-shop"></i>
    </div>
    <h1>Don Picaso</h1>
    <p class="subtitle">Sign in to manage your restaurants</p>

    <label for="email">Email</label>
    <input
      pInputText
      id="email"
      type="email"
      name="email"
      [(ngModel)]="email"
      required
      autocomplete="username"
    />

    <label for="password">Password</label>
    <p-password
      inputId="password"
      name="password"
      [(ngModel)]="password"
      [feedback]="false"
      [toggleMask]="true"
      [fluid]="true"
      autocomplete="current-password"
      required
    />

    @if (errorMessage()) {
      <p-message severity="error">{{ errorMessage() }}</p-message>
    }

    <p-button type="submit" label="Sign in" [loading]="isSubmitting()" [fluid]="true" />
  </form>
</div>
```

`frontend/src/app/features/auth/login/login.scss`:

```scss
.login-page {
  min-height: 100vh;
  display: grid;
  place-items: center;
  background: var(--p-primary-50);
}

.login-card {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  width: min(380px, 90vw);
  padding: 2.5rem 2rem;
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 8px 24px rgb(0 0 0 / 8%);

  .brand-mark {
    display: grid;
    place-items: center;
    width: 56px;
    height: 56px;
    margin: 0 auto;
    border-radius: 50%;
    background: var(--p-primary-100);

    i {
      font-size: 1.75rem;
      color: var(--p-primary-600);
    }
  }

  h1 {
    margin: 0;
    text-align: center;
    font-size: 1.5rem;
  }

  .subtitle {
    margin: 0 0 1rem;
    text-align: center;
    color: var(--p-surface-500);
  }

  label {
    font-weight: 600;
    font-size: 0.9rem;
  }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd frontend && npm test`
Expected: PASS — all three login tests (behavior tests prove the redesign didn't break submit/error handling).

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/features/auth/login
git commit -m "Redesign login page with PrimeNG card layout"
```

---

### Task 13: Staff PIN screen restyle (PrimeNG)

Behavior identical (roster select → PIN pad → submit); only the template/styles change, plus PrimeNG button imports.

**Files:**
- Modify: `frontend/src/app/features/auth/staff-login/staff-login.ts` (imports only)
- Modify: `frontend/src/app/features/auth/staff-login/staff-login.html`
- Modify: `frontend/src/app/features/auth/staff-login/staff-login.scss`

**Interfaces:**
- Consumes: PrimeNG `ButtonModule`. No API changes.
- Produces: nothing new.

- [ ] **Step 1: Add component imports**

`frontend/src/app/features/auth/staff-login/staff-login.ts` — add to the top imports and give the `@Component` decorator an imports array (it currently has none):

```ts
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
```

```ts
  imports: [ButtonModule, MessageModule],
```

- [ ] **Step 2: Rewrite the template**

`frontend/src/app/features/auth/staff-login/staff-login.html`:

```html
<div class="staff-login-page">
  @if (!selectedMember()) {
    <div class="panel roster">
      <h1>Who's working?</h1>
      <div class="roster-grid">
        @for (member of roster(); track member.userId) {
          <button type="button" class="roster-member" (click)="selectMember(member)">
            <span class="avatar">{{ member.displayName.charAt(0).toUpperCase() }}</span>
            <span>{{ member.displayName }}</span>
          </button>
        }
      </div>
    </div>
  } @else {
    <div class="panel pin-pad">
      <h1>{{ selectedMember()!.displayName }}</h1>
      <p class="pin-dots">{{ pin().padEnd(4, '•').split('').join(' ') }}</p>

      @if (errorMessage()) {
        <p-message severity="error">{{ errorMessage() }}</p-message>
      }

      <div class="digits">
        @for (digit of digits; track digit) {
          <p-button
            class="digit"
            [label]="digit"
            [rounded]="true"
            [outlined]="true"
            size="large"
            (onClick)="pressDigit(digit)"
          />
        }
      </div>

      <p-button
        class="enter-button"
        label="Enter"
        [fluid]="true"
        [disabled]="pin().length < 4"
        (onClick)="submitPin()"
      />
    </div>
  }
</div>
```

- [ ] **Step 3: Rewrite the styles**

`frontend/src/app/features/auth/staff-login/staff-login.scss`:

```scss
.staff-login-page {
  min-height: 100vh;
  display: grid;
  place-items: center;
  background: var(--p-primary-50);
}

.panel {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
  width: min(420px, 92vw);
  padding: 2.5rem 2rem;
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 8px 24px rgb(0 0 0 / 8%);

  h1 {
    margin: 0;
    font-size: 1.4rem;
  }
}

.roster-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 0.75rem;
  width: 100%;
}

.roster-member {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  padding: 1rem;
  background: #fff;
  border: 1px solid var(--p-surface-200);
  border-radius: 8px;
  cursor: pointer;

  &:hover {
    border-color: var(--p-primary-500);
  }

  .avatar {
    display: grid;
    place-items: center;
    width: 48px;
    height: 48px;
    border-radius: 50%;
    background: var(--p-primary-100);
    color: var(--p-primary-700);
    font-size: 1.25rem;
    font-weight: 700;
  }
}

.pin-dots {
  margin: 0;
  font-size: 1.75rem;
  letter-spacing: 0.5rem;
  color: var(--p-primary-700);
}

.digits {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 0.75rem;
  justify-items: center;

  // 0 sits centered on the fourth row, matching a phone keypad.
  .digit:last-child {
    grid-column: 2;
  }
}

.enter-button {
  width: 100%;
}
```

- [ ] **Step 4: Run tests + build**

Run: `cd frontend && npm test && npm run build`
Expected: the existing `staff-login.spec.ts` still passes untouched (same class API); build succeeds.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/features/auth/staff-login
git commit -m "Restyle staff PIN screen with PrimeNG"
```

---

### Task 14: Admin shell logout + README update

The logout button is a plain styled button, not PrimeNG — the admin area keeps its current look this phase (spec's explicit scope boundary).

**Files:**
- Modify: `frontend/src/app/features/admin/admin-shell/admin-shell.ts`
- Modify: `frontend/src/app/features/admin/admin-shell/admin-shell.html`
- Modify: `frontend/src/app/features/admin/admin-shell/admin-shell.scss`
- Modify: `frontend/src/app/features/admin/admin-shell/admin-shell.spec.ts`
- Modify: `README.md`

**Interfaces:**
- Consumes: `AuthService.logout()` (existing).
- Produces: nothing new.

- [ ] **Step 1: Write the failing test**

Add to `frontend/src/app/features/admin/admin-shell/admin-shell.spec.ts` (inside the existing `describe`, using its existing TestBed setup — if the file's setup lacks `provideRouter`, add `provideRouter([])` to its providers):

```ts
  it('logs out and navigates to the login page', async () => {
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');
    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('.logout-button') as HTMLButtonElement)!.click();
    await fixture.whenStable();

    expect(navigateSpy).toHaveBeenCalledWith('/login');
  });
```

(Import `Router` from `@angular/router` at the top if not present. `AuthService.logout()` with clean localStorage clears locally without an HTTP call, so no request needs flushing — but if the suite uses `httpMock.verify()`, run `localStorage.clear()` in `beforeEach`.)

Run: `cd frontend && npm test`
Expected: new test FAILS (no `.logout-button`).

- [ ] **Step 2: Implement logout in the shell**

`frontend/src/app/features/admin/admin-shell/admin-shell.ts` — full replacement:

```ts
import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';

import { Role } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-admin-shell',
  imports: [RouterLink, RouterOutlet],
  templateUrl: './admin-shell.html',
  styleUrl: './admin-shell.scss',
})
export class AdminShell {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly currentUser = this.authService.currentUser;
  protected readonly Role = Role;

  protected async logout(): Promise<void> {
    await this.authService.logout();
    // Not awaited: in tests (and any router config missing '/login')
    // navigateByUrl's promise rejects on no-match, which would otherwise
    // propagate out of this method. Matches the existing pattern in
    // staff-login.ts and pos-shell.ts for the same reason.
    void this.router.navigateByUrl('/login');
  }
}
```

`frontend/src/app/features/admin/admin-shell/admin-shell.html` — wrap the existing nav in a header with the user identity and logout button (keep the nav links exactly as they are):

```html
<header class="admin-header">
  <nav class="admin-nav">
    @if (currentUser()?.role === Role.Corporate) {
      <a routerLink="/admin/brands">Brands</a>
    }
    @if (currentUser()?.role === Role.BrandOwner && currentUser()?.brandId) {
      <a [routerLink]="['/admin/brands', currentUser()!.brandId!, 'branches']">Branches</a>
    }
    @if (currentUser()?.role === Role.BranchManager && currentUser()?.branchId) {
      <a [routerLink]="['/admin/branches', currentUser()!.branchId!, 'users']">Users</a>
    }
  </nav>

  <div class="session-controls">
    @if (currentUser(); as user) {
      <span class="current-role">{{ user.role }}</span>
    }
    <button type="button" class="logout-button" (click)="logout()">Log out</button>
  </div>
</header>

<router-outlet />
```

`frontend/src/app/features/admin/admin-shell/admin-shell.scss` — append:

```scss
.admin-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.session-controls {
  display: flex;
  align-items: center;
  gap: 0.75rem;

  .current-role {
    color: #666;
    font-size: 0.9rem;
  }

  .logout-button {
    background: none;
    border: 1px solid #ccc;
    border-radius: 4px;
    padding: 0.35rem 0.75rem;
    cursor: pointer;

    &:hover {
      border-color: #999;
    }
  }
}
```

- [ ] **Step 3: Run tests to verify they pass**

Run: `cd frontend && npm test`
Expected: PASS.

- [ ] **Step 4: Update README**

`README.md` changes:

1. Solution structure block — add under `Modules.Sales/`: `    Modules.Menu/              Menu catalog (categories, products) read by the POS` and under tests: `  Modules.Menu.Tests/`.
2. Tech stack frontend line — change to: `- **Frontend:** Angular 21 (standalone components, signals, template-driven forms), PrimeNG, SCSS, Vitest`
3. Frontend structure — replace the `features/pos/` line with: `` - `features/pos/` — POS ordering screen: product catalog, cart, payment dialog `` and add: `` - `core/menu/` — brand menu read model with localStorage offline fallback ``.

- [ ] **Step 5: Full-stack verification**

```bash
dotnet build && dotnet test
cd frontend && npm test && npm run build
```

Expected: everything green.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/app/features/admin/admin-shell README.md
git commit -m "Add logout to admin shell and update README"
```

---

## Spec Coverage Map

| Spec section | Tasks |
|---|---|
| Modules.Menu (entities, GetMenu, seeding) | 1, 2, 3 |
| Order payment extension + server-side money validation | 4 |
| PrimeNG setup (green Aura preset) | 5 |
| MenuService offline cache | 6 |
| CartService money math | 7 |
| POS screen (grid/search/tabs, cart panel, payment dialog, shell) | 8, 9, 10, 11 |
| Logout (POS → /staff-login, admin → /login) | 11, 14 |
| Login redesign + staff PIN restyle | 12, 13 |
| Testing (backend + frontend) | every task |
