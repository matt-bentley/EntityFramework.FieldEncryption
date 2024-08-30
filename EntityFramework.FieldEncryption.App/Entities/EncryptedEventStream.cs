using EntityFramework.FieldEncryption.DomainEvents.Abstract;
using EntityFramework.FieldEncryption.Encryption;
using EntityFramework.FieldEncryption.Entities.Abstract;

namespace EntityFramework.FieldEncryption.Entities
{
    public sealed class EncryptedEventStream : Entity
    {
        private EncryptedEventStream(Guid id,
            EncryptedData<List<DomainEvent>> events,
            DateTime createdDate,
            int version) : base(id)
        {
            Events = events;
            CreatedDate = createdDate;
            Version = version;
        }

        public static EncryptedEventStream Create(Guid id, DomainEvent createdEvent, CompressionOptions options)
        {
            var events = new EncryptedData<List<DomainEvent>>([createdEvent], options);
            return new EncryptedEventStream(id, events, DateTime.UtcNow, 1);
        }

        public EncryptedData<List<DomainEvent>> Events { get; private set; }
        public DateTime CreatedDate { get; private set; }
        public int Version { get; private set; }

        public void AddEvent(DomainEvent @event)
        {
            Events.Data.Add(@event);
            Version++;
        }
    }
}
