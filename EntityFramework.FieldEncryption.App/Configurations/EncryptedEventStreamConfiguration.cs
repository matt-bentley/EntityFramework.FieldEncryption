using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using EntityFramework.FieldEncryption.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using EntityFramework.FieldEncryption.DomainEvents.Abstract;
using System.Text.Json;
using EntityFramework.FieldEncryption.Encryption;

namespace EntityFramework.FieldEncryption.Configurations
{
    public sealed class EncryptedEventStreamConfiguration : IEntityTypeConfiguration<EncryptedEventStream>
    {
        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions();

        public void Configure(EntityTypeBuilder<EncryptedEventStream> builder)
        {
            builder.Property(e => e.Id)
                   .ValueGeneratedNever();

            Func<EncryptedData<List<DomainEvent>>, EncryptedData<List<DomainEvent>>, bool> equalityComparer = (left, right) =>
            {
                if (left.Data.Count != right.Data.Count)
                {
                    return false;
                }
                for (int i = 0; i < left.Data.Count; i++)
                {
                    if (right.Data[i].EventId != left.Data[i].EventId)
                    {
                        return false;
                    }
                }
                return true;
            };

            Func<EncryptedData<List<DomainEvent>>, int> hashCodeGenerator = (events) =>
            {
                if (events == null || events.Data.Count == 0)
                {
                    return 0;
                }
                var hashcode = new HashCode();
                for (int i = 0; i < events.Data.Count; i++)
                {
                    hashcode.Add(events.Data[i].EventId);
                }
                return hashcode.ToHashCode();
            };

            var comparer = new ValueComparer<EncryptedData<List<DomainEvent>>>(
                (l, r) => equalityComparer.Invoke(l, r),
                events => hashCodeGenerator.Invoke(events),
                v => JsonSerializer.Deserialize<EncryptedData<List<DomainEvent>>>(JsonSerializer.Serialize(v, _serializerOptions), _serializerOptions));

            var converter = new ValueConverter<EncryptedData<List<DomainEvent>>, byte[]>(
                v => Compressor.Compress(JsonSerializer.Serialize(v.Data, _serializerOptions), v.Options),
                v => Decompress(v));


            var eventsProperty = builder.Property(e => e.Events)
                                        .HasConversion(converter)
                                        .IsRequired();

            eventsProperty.Metadata.SetValueConverter(converter);
            eventsProperty.Metadata.SetValueComparer(comparer);
        }

        private static EncryptedData<List<DomainEvent>> Decompress(byte[] encrypted)
        {
            var json = Compressor.Decompress(encrypted, out CompressionOptions option);
            var events = JsonSerializer.Deserialize<List<DomainEvent>>(json, _serializerOptions);
            return new EncryptedData<List<DomainEvent>>(events, option);
        }
    }
}
