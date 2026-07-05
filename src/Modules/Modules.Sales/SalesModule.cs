using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Modules.Sales.Features.Orders.CreateOrder;
using Modules.Sales.Persistence;

namespace Modules.Sales;

/// <summary>
/// Composition root for the Sales module. The host (API bootstrap project)
/// calls these two methods; everything else stays internal to the module.
/// </summary>
public static class SalesModule
{
    public static IServiceCollection AddSalesModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SalesDbContext>(options => options.UseNpgsql(connectionString));

        services.AddValidatorsFromAssembly(typeof(SalesModule).Assembly, includeInternalTypes: true);
        services.AddScoped<CreateOrderCommandHandler>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }

    public static IEndpointRouteBuilder MapSalesModule(this IEndpointRouteBuilder app)
    {
        app.MapCreateOrder();
        return app;
    }
}
