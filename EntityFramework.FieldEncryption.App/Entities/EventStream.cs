using EntityFramework.FieldEncryption.DomainEvents.Abstract;
using EntityFramework.FieldEncryption.Entities.Abstract;

namespace EntityFramework.FieldEncryption.Entities
{
    public sealed class EventStream : Entity
    {
        private EventStream(Guid id, 
            List<DomainEvent> events,
            DateTime createdDate,
            int version) : base(id)
        {
            Events = events;
            CreatedDate = createdDate;
            Version = version;
        }

        public static EventStream Create(Guid id, DomainEvent createdEvent)
        {
            return new EventStream(id, [createdEvent], DateTime.UtcNow, 1);
        }

        public List<DomainEvent> Events { get; private set; }
        public DateTime CreatedDate { get; private set; }
        public int Version { get; private set; }

        public void AddEvent(DomainEvent @event)
        {
            Events.Add(@event);
            Version++;
        }
    }
}
