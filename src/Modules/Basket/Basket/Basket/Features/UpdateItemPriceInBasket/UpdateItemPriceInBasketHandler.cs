
namespace Basket.Basket.Features.UpdateItemPriceInBasket;

public record UpdateItemPriceinBasketCommand(Guid ProductId, decimal Price)
    :ICommand<UpdateItemPriceinBasketResult>;

public record UpdateItemPriceinBasketResult(bool IsSuccess);

public class UpdateItemPriceinBasketValidator : AbstractValidator<UpdateItemPriceinBasketCommand>
{
    public UpdateItemPriceinBasketValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("ProductId is required");
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price must be greater than 0");
    }
}
public class UpdateItemPriceInBasketHandler (BasketDbContext dbContext)
    : ICommandHandler<UpdateItemPriceinBasketCommand, UpdateItemPriceinBasketResult>
{
    public async Task<UpdateItemPriceinBasketResult> Handle(UpdateItemPriceinBasketCommand command, CancellationToken cancellationToken)
    {
       var itemsToUpdate = await dbContext.ShoppingCartItems
            .Where(x=>x.ProductId== command.ProductId)
            .ToListAsync(cancellationToken);

        if(!itemsToUpdate.Any())
            return new UpdateItemPriceinBasketResult(false);

        foreach (var item in itemsToUpdate)
        {
            item.UpdatePrice(command.Price);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new UpdateItemPriceinBasketResult(true);


    }
}
