

namespace Basket.Basket.Features.GetBasket;

public record GetBasketQuery(string UserName):ICommand<GetBasketResult>;

public record GetBasketResult(ShoppingCartDto ShoppingCart);

public class GetBasketQueryValidator : AbstractValidator<GetBasketQuery>
{
    public GetBasketQueryValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().WithMessage("UserName is Required");
    }
}
public class GetBasketHandler(IBasketRepository repository) : ICommandHandler<GetBasketQuery, GetBasketResult>
{
    public async Task<GetBasketResult> Handle(GetBasketQuery query, CancellationToken cancellationToken)
    {
        var basket =await repository.GetBasket(query.UserName,true, cancellationToken: cancellationToken);

        var basketDto = basket.Adapt<ShoppingCartDto>();

        return new GetBasketResult(basketDto);
    }
}
