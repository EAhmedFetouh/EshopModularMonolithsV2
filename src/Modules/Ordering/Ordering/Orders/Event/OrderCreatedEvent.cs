
using Ordering.Orders.Models;
using Shared.DDD;

namespace Ordering.Orders.Event;

public record OrderCreatedEvent(Order  Order): IDomainEvent;
