using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.Identity;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;
using Modules.Menu;
using Modules.Menu.Persistence;
using Modules.Sales;

var builder = WebApplication.CreateBuilder(args);

const string PosCorsPolicy = "PosTablets";

builder.Services.AddProblemDetails();

// Process-wide: every enum crosses the wire as a string ("Cash", "Staff",
// ...), matching the Angular payload types. Added for Sales' PaymentMethod,
// but this also governs Identity's UserRole (and any future enum) since
// ConfigureHttpJsonOptions applies to all minimal-API JSON binding/output
// in the host, not just the module that motivated it.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

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

builder.Services.AddMenuModule(
    builder.Configuration.GetConnectionString("MenuDb")
        ?? throw new InvalidOperationException("Connection string 'MenuDb' is not configured."),
    builder.Configuration.GetSection("Menu").Get<MenuOptions>()
        ?? throw new InvalidOperationException("'Menu' configuration section is not configured."));

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

    using var seedScope = app.Services.CreateScope();
    await IdentitySeeder.SeedAsync(
        seedScope.ServiceProvider.GetRequiredService<IdentityDbContext>(),
        seedScope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(),
        TimeProvider.System);

    // The menu seeder needs the seeded brand's id. Only the host may bridge
    // the two modules' contexts — they never reference each other.
    var seedBrandId = await seedScope.ServiceProvider.GetRequiredService<IdentityDbContext>()
        .Brands.Select(b => b.Id).FirstAsync();

    await MenuSeeder.SeedAsync(
        seedScope.ServiceProvider.GetRequiredService<MenuDbContext>(),
        seedBrandId);
}

app.MapSalesModule();
app.MapMenuModule();
app.MapIdentityModule();

app.Run();
