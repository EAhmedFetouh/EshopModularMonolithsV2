
using Mapster;

namespace Ordering.Orders.Features.GetOrderById;

public record GetOrderByIdQuery(Guid Id): IQuery<GetOrderByIdResult>;

public record GetOrderByIdResult(OrderDto Order);

public class GetOrderByIdValidator: AbstractValidator<GetOrderByIdQuery>
{
    public GetOrderByIdValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required");
    }
}

internal class GetOrderByIdHandler (OrderingDbContext dbContext): IQueryHandler<GetOrderByIdQuery, GetOrderByIdResult>
{
    public async Task<GetOrderByIdResult> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken)
    {
       var order = await dbContext.Orders
            .AsNoTracking()
            .Include(o=> o.Items)
            .SingleOrDefaultAsync(p=> p.Id == query.Id, cancellationToken);

        if (order is null)
            throw new OrderNotFoundException(query.Id);

        var orderDto = order.Adapt<OrderDto>();

        return new GetOrderByIdResult(orderDto);
    }
}
