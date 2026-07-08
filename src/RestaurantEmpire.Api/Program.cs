using Microsoft.AspNetCore.Identity;
using Modules.Identity;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;
using Modules.Menu;
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
}

app.MapSalesModule();
app.MapMenuModule();
app.MapIdentityModule();

app.Run();
