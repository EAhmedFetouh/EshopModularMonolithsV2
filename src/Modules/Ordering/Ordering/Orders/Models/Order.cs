using Ordering.Orders.Event;
using Ordering.Orders.ValueObjects;
using Shared.DDD;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ordering.Orders.Models;

public class Order : Aggregate<Guid>
{
    private readonly List<OrderItem> _items = new();
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    public Guid CustomerId { get; private set; }
    public string OrderName { get; private set; } = default!;

    public Address ShippingAdress { get; private set; } = default!;
    public Address BillingAddress { get; private set; } = default!;
    public Payment Payment { get; private set; } = default!;

    public decimal TotalPrice { get; private set; }

    public static Order Create(Guid id, Guid customerId, string orderName, Address shippingAddress, Address billingAddress, Payment payment)
    {
        var order = new Order
        {
            Id = id,
            CustomerId = customerId,
            OrderName = orderName,
            ShippingAdress = shippingAddress,
            BillingAddress = billingAddress,
            Payment = payment,
            TotalPrice = 0
        };

        order.AddDomainEvent(new OrderCreatedEvent(order));

        return order;
    }

    public void Add(Guid productId, int quantity, decimal price)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);

        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);

        if (existingItem != null)
        {
            existingItem.Quantity += quantity;
        }
        else
        {
            var newItem = new OrderItem(Id, productId, price, quantity);
            _items.Add(newItem);
        }

        // Recalculate total price
        TotalPrice = _items.Sum(item => item.Price * item.Quantity);
    }


    public void Remove(Guid productId)
    {
        var orderItem = _items.FirstOrDefault(i => i.ProductId == productId);
        if (orderItem != null)
        {
            _items.Remove(orderItem);
            // Recalculate total price
            TotalPrice = _items.Sum(item => item.Price * item.Quantity);
        }
    }
}
