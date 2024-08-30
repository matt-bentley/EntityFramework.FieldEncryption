using System.Text.Json.Serialization;

namespace EntityFramework.FieldEncryption.DomainEvents.Abstract
{
    [JsonDerivedType(typeof(ProductCreatedEvent), nameof(ProductCreatedEvent))]
    [JsonDerivedType(typeof(ProductUpdatedEvent), nameof(ProductUpdatedEvent))]
    public abstract class DomainEvent
    {
        public DomainEvent()
        {
            EventId = Guid.NewGuid();
            Timestamp = DateTime.UtcNow;
        }

        public Guid EventId { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
