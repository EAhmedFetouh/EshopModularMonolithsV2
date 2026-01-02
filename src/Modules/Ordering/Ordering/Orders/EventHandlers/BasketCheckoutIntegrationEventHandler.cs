using MassTransit;
using Microsoft.Extensions.Logging;
using Ordering.Orders.Features.CreateOrder;
using Shared.Messaing.Events;

namespace Ordering.Orders.EventHandlers;

public class BasketCheckoutIntegrationEventHandler 
    (ISender sender, ILogger<BasketCheckoutIntegrationEventHandler> logger)
    : IConsumer<BasketCheckoutIntegrationEvent>
{
    public async Task Consume(ConsumeContext<BasketCheckoutIntegrationEvent> context)
    {
        logger.LogInformation("Integration Event handled : {IntegrationEvent}", context.Message.GetType().Name);

        // Log the incoming message properties for debugging
        logger.LogDebug("Incoming BasketCheckoutIntegrationEvent: {@Message}", context.Message);

        var createOrderCommand = MapToCreateOrderCommand(context.Message);

        // Log the mapped order DTO to ensure required fields are present
        logger.LogDebug("Mapped CreateOrderCommand.Order: {@Order}", createOrderCommand.Order);

        await sender.Send(createOrderCommand);

    }

    private CreateOrderCommand MapToCreateOrderCommand(
    BasketCheckoutIntegrationEvent message)
    {
        // Create full order with incoming event data
        var addressDto = new AddressDto(
            message.FirstName,
            message.LastName,
            message.EmailAddress,
            message.Country,
            message.State,
            message.ZipCode,
            message.AddressLine
        );

        var paymentDto = new PaymentDto(
            message.CardName,
            message.CardNumber,
            message.Expiration,
            message.Cvv,
            message.PaymentMethod
        );

        var orderId = Guid.NewGuid();

        var orderDto = new OrderDto(
            Id: orderId,
            CustomerId: message.CustomerId,
            OrderName: message.UserName,
            ShippingAddress: addressDto,
            BillingAddress: addressDto,
            Payment: paymentDto,
            Items:
            [
                new OrderItemDto(
                orderId,
                new Guid("5334c996-8457-4cf0-815c-ed2b77c4ff61"),2,500
            ),
            new OrderItemDto(
                orderId,
                new Guid("c67d6323-e8b1-4bdf-9a75-b0d0d2e7e914"),1,400
            )
            ]
        );

        return new CreateOrderCommand(orderDto);
    }

}
