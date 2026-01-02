using System.Runtime.CompilerServices;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Logging;
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

internal class CheckoutBasketHandler (IBasketRepository repository, IBus bus, BasketDbContext dbcontext, ILogger<CheckoutBasketHandler> logger) : ICommandHandler<CheckoutBasketCommand, CheckoutBasketResult>
{
    public async Task<CheckoutBasketResult> Handle(CheckoutBasketCommand command, CancellationToken cancellationToken)
    {


        // CHECKOUT WITH OUTBOX PATTERN
        await using var transaction = await dbcontext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            
            var basket = await dbcontext.ShoppingCarts
             .Include(b => b.Items)
             .SingleOrDefaultAsync(x=>x.UserName == command.BasketCheckout.UserName, cancellationToken);

            if (basket == null || !basket.Items.Any())
            {
                throw new BasketNotFoundException("Basket is empty or does not exist.");
            }

            var eventMessage = command.BasketCheckout.Adapt<BasketCheckoutIntegrationEvent>();
            eventMessage.TotalPrice = basket.TotalProice;

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = typeof(BasketCheckoutIntegrationEvent).AssemblyQualifiedName!,
                Content = JsonSerializer.Serialize(eventMessage),
                OccurredOn = DateTime.UtcNow
            };
            dbcontext.OutboxMessages.Add(outboxMessage);

            dbcontext.ShoppingCarts.Remove(basket);

            await dbcontext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new CheckoutBasketResult(true);

        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, "Error during checkout for user {UserName}", command.BasketCheckout.UserName);
            return new CheckoutBasketResult(false);
        }



        // CHECKOUT WITH OUT OUTBOX PATTERN
        // var basket = await repository
        //     .GetBasket(command.BasketCheckout.UserName, false, cancellationToken: cancellationToken);

        // if (basket == null || !basket.Items.Any())
        //     throw new InvalidOperationException("Basket is empty or does not exist.");

        // var eventMessage = command.BasketCheckout.Adapt<BasketCheckoutIntegrationEvent>();
        // eventMessage.TotalPrice = basket.TotalProice;

        // await bus.Publish(eventMessage, cancellationToken);

        // await repository.DeleteBasket(command.BasketCheckout.UserName, cancellationToken);

        // return new CheckoutBasketResult(true);
    }
}
