
using Basket.Basket.Features.UpdateItemPriceInBasket;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaing.Events;

namespace Basket.Basket.EventHandlers;

public class ProductPriceChangedIntegrationEventHandler 
    (ISender sender,ILogger<ProductPriceChangedIntegrationEventHandler> logger)
    : IConsumer<ProductPriceChangedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ProductPriceChangedIntegrationEvent> context)
    {
       logger.LogInformation("Integration Event Handled: {IntegrationEvent}", context.Message.GetType().Name);

        var command= new UpdateItemPriceinBasketCommand(context.Message.ProductId,context.Message.Price);
        var result = await sender.Send(command);

        if (!result.IsSuccess)
            logger.LogError("Error updateing price in basket for product id: {ProductId}", context.Message.ProductId);


        logger.LogInformation("Price for product id: {ProductId} updated in basket:", context.Message.ProductId);

     
    }
}
