
namespace Shared.Messaing.Events;

public class IntegrationEvent
{
    public Guid EventId => Guid.NewGuid();
    public DateTime OccurredOn => DateTime.Now;

    public string EventType => GetType().AssemblyQualifiedName;
}
