using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using EntityFramework.FieldEncryption.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using EntityFramework.FieldEncryption.DomainEvents.Abstract;
using System.Text.Json;

namespace EntityFramework.FieldEncryption.Configurations
{
    public sealed class EventStreamConfiguration : IEntityTypeConfiguration<EventStream>
    {
        public void Configure(EntityTypeBuilder<EventStream> builder)
        {
            builder.Property(e => e.Id)
                   .ValueGeneratedNever();

            Func<List<DomainEvent>, List<DomainEvent>, bool> equalityComparer = (left, right) =>
            {
                if (left.Count != right.Count)
                {
                    return false;
                }
                for (int i = 0; i < left.Count; i++)
                {
                    if (right[i].EventId != left[i].EventId)
                    {
                        return false;
                    }
                }
                return true;
            };

            Func<List<DomainEvent>, int> hashCodeGenerator = (events) =>
            {
                if (events == null || events.Count == 0)
                {
                    return 0;
                }
                var hashcode = new HashCode();
                for (int i = 0; i < events.Count; i++)
                {
                    hashcode.Add(events[i].EventId);
                }
                return hashcode.ToHashCode();
            };

            var serializerOptions = new JsonSerializerOptions();

            var comparer = new ValueComparer<List<DomainEvent>>(
                (l, r) => equalityComparer.Invoke(l, r),
                events => hashCodeGenerator.Invoke(events),
                v => JsonSerializer.Deserialize<List<DomainEvent>>(JsonSerializer.Serialize(v, serializerOptions), serializerOptions));

            var converter = new ValueConverter<List<DomainEvent>, string>(
                v => JsonSerializer.Serialize(v, serializerOptions),
                v => JsonSerializer.Deserialize<List<DomainEvent>>(v, serializerOptions));


            var eventsProperty = builder.Property(e => e.Events)
                                        .IsUnicode(false)
                                        .HasConversion(converter)
                                        .IsRequired();

            eventsProperty.Metadata.SetValueConverter(converter);
            eventsProperty.Metadata.SetValueComparer(comparer);
        }
    }
}
