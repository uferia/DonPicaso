using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Modules.Sales.Features.Orders.CreateOrder;

public static class CreateOrderEndpoint
{
    public static IEndpointRouteBuilder MapCreateOrder(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/orders", async (
                CreateOrderCommand command,
                IValidator<CreateOrderCommand> validator,
                CreateOrderCommandHandler handler,
                CancellationToken cancellationToken) =>
            {
                var validation = await validator.ValidateAsync(command, cancellationToken);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(validation.ToDictionary());
                }

                var orderId = await handler.HandleAsync(command, cancellationToken);
                return Results.Created($"/api/v1/orders/{orderId}", new CreateOrderResponse(orderId));
            })
            .WithName("CreateOrder")
            .WithTags("Orders")
            .Produces<CreateOrderResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        return app;
    }
}

public sealed record CreateOrderResponse(Guid OrderId);
