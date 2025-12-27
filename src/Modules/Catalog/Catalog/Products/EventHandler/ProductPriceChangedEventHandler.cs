
using MassTransit;
using Shared.Messaing.Events;

namespace Catalog.Products.EventHandler;

public class ProductPriceChangedEventHandler (IBus bus,ILogger<ProductPriceChangedEventHandler> logger)
    : INotificationHandler<ProductPriceChangedEvent>
{
    public async Task Handle(ProductPriceChangedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event handeled: {DomainEvent}", notification.GetType().Name);

        var integrationEvent = new ProductPriceChangedIntegrationEvent
        {
            ProductId = notification.product.Id,
            Name = notification.product.Name,
            Description = notification.product.Description,
            ImageFile = notification.product.ImageFile,
            category = notification.product.Category,
            Price = notification.product.Price, // Set updated product price
        };

        await bus.Publish(integrationEvent,cancellationToken);

    }
}
