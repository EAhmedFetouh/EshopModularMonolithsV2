using System.Security.Claims;

namespace Basket.Basket.Features.CheckoutBasket;

public record CheckoutBasketRequest(BasketCheckoutDto BasketCheckout) : ICommand<CheckoutBasketResponse>;

public record CheckoutBasketResponse(bool IsSuccess);

public class CheckoutBasketEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/basket/checkout", async (
                CheckoutBasketRequest request,
                ISender sender,
                ClaimsPrincipal user) =>
            {
                var userName = user.Identity?.Name;

                var updatedBasketCheckout = request.BasketCheckout with
                {
                    UserName = userName!
                };

                var command = new CheckoutBasketCommand(updatedBasketCheckout);
                var result = await sender.Send(command);
                var response = result.Adapt<CheckoutBasketResponse>();
                return Results.Ok(response);
            })
            .WithName("CheckoutBasket")
            .Produces<CheckoutBasketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Checkout Basket")
            .WithDescription("Checkout Basket")
            .RequireAuthorization();
    }
}
