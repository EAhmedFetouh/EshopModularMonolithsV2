

using MassTransit;
using Shared.Messaing.Events;

namespace Basket.Basket.Features.CheckoutBasket;

public record CheckoutBasketCommand(BasketCheckoutDto BasketCheckout) : ICommand<CheckoutBasketResult>;

public record CheckoutBasketResult(bool IsSuccess);

public class CheckoutBasketCommandValidator : AbstractValidator<CheckoutBasketCommand>
{
    public CheckoutBasketCommandValidator()
    {
        RuleFor(x => x.BasketCheckout).NotNull().WithMessage("BasketCheckoutDto can't be null");
        RuleFor(x => x.BasketCheckout.UserName).NotEmpty().WithMessage("UserName is required");
    }
}

internal class CheckoutBasketHandler (IBasketRepository repository, IBus bus) : ICommandHandler<CheckoutBasketCommand, CheckoutBasketResult>
{
    public async Task<CheckoutBasketResult> Handle(CheckoutBasketCommand command, CancellationToken cancellationToken)
    {
        var basket = await repository
            .GetBasket(command.BasketCheckout.UserName, false, cancellationToken: cancellationToken);

        if (basket == null || !basket.Items.Any())
            throw new InvalidOperationException("Basket is empty or does not exist.");

        var eventMessage = command.BasketCheckout.Adapt<BasketCheckoutIntegrationEvent>();
        eventMessage.TotalPrice = basket.TotalProice;

        await bus.Publish(eventMessage, cancellationToken);

        await repository.DeleteBasket(command.BasketCheckout.UserName, cancellationToken);

        return new CheckoutBasketResult(true);
    }
}
