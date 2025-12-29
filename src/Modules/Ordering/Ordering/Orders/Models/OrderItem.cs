using Shared.DDD;

namespace Ordering.Orders.Models;

public class OrderItem :Entity<Guid>
{
    internal OrderItem(Guid orderId,Guid productId, decimal price, int quantity)
    {
        OrderId = orderId;
        ProductId = productId;
        Price = price;
        Quantity = quantity;
    }

    public Guid OrderId { get; private set; } = default!;
    public Guid ProductId { get; private set; } = default!;
    public int Quantity { get; internal set; } = default!;
    public decimal Price { get; private set; } = default!;
}
