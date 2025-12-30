
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

public class CreateOrderHandler (OrderingDbContext dbContext): ICommandHandler<CreateOrderCommand, CreateOrderResult>
{
    public async Task<CreateOrderResult> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var order = CreateNewOrder(command.Order);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CreateOrderResult(order.Id);
    }

    private Order CreateNewOrder(OrderDto orderDto)
    {
      var shippingAddress = Address.Of(orderDto.ShippingAddress.FirstName,orderDto.ShippingAddress.LastName,orderDto.ShippingAddress.EmailAddress,orderDto.ShippingAddress.AddressLine, orderDto.ShippingAddress.State,orderDto.ShippingAddress.Country, orderDto.ShippingAddress.ZipCode);
      var billingAddress = Address.Of(orderDto.ShippingAddress.FirstName,orderDto.ShippingAddress.LastName,orderDto.ShippingAddress.EmailAddress,orderDto.ShippingAddress.AddressLine, orderDto.ShippingAddress.State,orderDto.ShippingAddress.Country, orderDto.ShippingAddress.ZipCode);

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
