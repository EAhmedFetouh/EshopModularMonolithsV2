using Microsoft.Extensions.Logging;

namespace Ordering.Orders.Features.CreateOrder;

public record CreateOrderCommand(OrderDto Order): ICommand<CreateOrderResult>;

public record CreateOrderResult(Guid Id);


internal class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.Order.OrderName).NotEmpty().WithMessage("OrderName is required");
    }
}

public class CreateOrderHandler (OrderingDbContext dbContext, ILogger<CreateOrderHandler> logger): ICommandHandler<CreateOrderCommand, CreateOrderResult>
{
    public async Task<CreateOrderResult> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        if (command.Order is null)
        {
            logger.LogError("CreateOrderCommand.Order is null");
            throw new ArgumentNullException(nameof(command.Order));
        }

        logger.LogInformation("CreateOrderHandler: Creating order for customer {CustomerId} - OrderName={OrderName}", command.Order.CustomerId, command.Order.OrderName);

        try
        {
            var order = CreateNewOrder(command.Order);
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("CreateOrderHandler: Order created with Id={OrderId}", order.Id);
            return new CreateOrderResult(order.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CreateOrderHandler: Error creating order for customer {CustomerId}", command.Order.CustomerId);
            throw;
        }
    }

    private Order CreateNewOrder(OrderDto orderDto)
    {
        // Validate required address fields
        if (string.IsNullOrWhiteSpace(orderDto.ShippingAddress?.FirstName) || string.IsNullOrWhiteSpace(orderDto.ShippingAddress?.LastName))
        {
            throw new ArgumentException("Shipping address first name and last name are required.");
        }
        if (orderDto.BillingAddress == null || string.IsNullOrWhiteSpace(orderDto.BillingAddress.LastName) || string.IsNullOrWhiteSpace(orderDto.BillingAddress.FirstName))
        {
            throw new ArgumentException("Billing address first name and last name are required.");
        }

        var shippingAddress = Address.Of(
            orderDto.ShippingAddress.FirstName,
            orderDto.ShippingAddress.LastName,
            orderDto.ShippingAddress.EmailAddress,
            orderDto.ShippingAddress.AddressLine,
            orderDto.ShippingAddress.State,
            orderDto.ShippingAddress.Country,
            orderDto.ShippingAddress.ZipCode);

        // Use billing address from OrderDto (previously incorrectly used ShippingAddress twice)
        var billingAddress = Address.Of(
            orderDto.BillingAddress.FirstName,
            orderDto.BillingAddress.LastName,
            orderDto.BillingAddress.EmailAddress,
            orderDto.BillingAddress.AddressLine,
            orderDto.BillingAddress.State,
            orderDto.BillingAddress.Country,
            orderDto.BillingAddress.ZipCode);

        var newOrder = Order.Create(
            Guid.NewGuid(),
            orderDto.CustomerId,
            orderDto.OrderName,
            shippingAddress,
            billingAddress,
            Payment.Of(orderDto.Payment.CardName,orderDto.Payment.CardNumber,orderDto.Payment.Expiration,orderDto.Payment.Cvv, orderDto.Payment.PaymentMethod)
        );

        orderDto.Items.ForEach(item =>
        {
            newOrder.Add(item.ProductId, item.Quantity, item.Price);
        });

        return newOrder;
    }
}
