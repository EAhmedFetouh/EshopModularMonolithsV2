
namespace Shared.Messaing.Events;

public record ProductPriceChangedIntegrationEvent : IntegrationEvent
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = default!;
    public List<string> category { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string ImageFile { get; set; } = default!;
    public decimal Price { get; set; } = default!;
}
