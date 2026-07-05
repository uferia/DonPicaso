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

                var result = await handler.HandleAsync(command, cancellationToken);
                var response = new CreateOrderResponse(result.OrderId);

                // A replayed order returns 200 with the original id so the
                // sync client treats it as success and clears its local copy.
                return result.WasAlreadyProcessed
                    ? Results.Ok(response)
                    : Results.Created($"/api/v1/orders/{result.OrderId}", response);
            })
            .WithName("CreateOrder")
            .WithTags("Orders")
            .Produces<CreateOrderResponse>(StatusCodes.Status201Created)
            .Produces<CreateOrderResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        return app;
    }
}

public sealed record CreateOrderResponse(Guid OrderId);
