using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Menu.Features.Catalog.GetMenu;
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
        services.AddScoped<GetMenuQueryHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapMenuModule(this IEndpointRouteBuilder app)
    {
        app.MapGetMenu();

        return app;
    }
}
