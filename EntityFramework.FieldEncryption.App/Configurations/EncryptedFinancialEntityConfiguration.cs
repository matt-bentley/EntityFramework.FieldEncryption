using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using EntityFramework.FieldEncryption.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using EntityFramework.FieldEncryption.Encryption;

namespace EntityFramework.FieldEncryption.Configurations
{
    public sealed class EncryptedFinancialEntityConfiguration : IEntityTypeConfiguration<EncryptedFinancialEntity>
    {
        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions();

        public void Configure(EntityTypeBuilder<EncryptedFinancialEntity> builder)
        {
            builder.Property(e => e.Id)
                   .ValueGeneratedNever();

            builder.Property(e => e.Name)
                   .HasMaxLength(128)
                   .IsRequired();

            builder.Property(e => e.TaxIdentificationNumber)
                   .IsUnicode(false)
                   .HasMaxLength(32)
                   .IsRequired();

            Func<EncryptedData<Dictionary<string, decimal>>, EncryptedData<Dictionary<string, decimal>>, bool> equalityComparer = (left, right) =>
            {
                return left.Data.Keys.Count == right.Data.Keys.Count
                    && left.Data.Keys.All(key => right.Data.TryGetValue(key, out decimal value) && left.Data[key] == value);
            };

            Func<EncryptedData<Dictionary<string, decimal>>, int> hashCodeGenerator = (amounts) =>
            {
                return amounts.Data.GetHashCode();
            };

            var comparer = new ValueComparer<EncryptedData<Dictionary<string, decimal>>>(
                (l, r) => equalityComparer.Invoke(l, r),
                amounts => hashCodeGenerator.Invoke(amounts),
                v => JsonSerializer.Deserialize<EncryptedData<Dictionary<string, decimal>>>(JsonSerializer.Serialize(v, _serializerOptions), _serializerOptions));

            var converter = new ValueConverter<EncryptedData<Dictionary<string, decimal>>, byte[]>(
                v => Compressor.Compress(JsonSerializer.Serialize(v.Data, _serializerOptions), v.Options),
                v => Decompress(v));

            var eventsProperty = builder.Property(e => e.Amounts)
                                        .IsUnicode(false)
                                        .HasConversion(converter)
                                        .IsRequired();

            eventsProperty.Metadata.SetValueConverter(converter);
            eventsProperty.Metadata.SetValueComparer(comparer);
        }

        private static EncryptedData<Dictionary<string, decimal>> Decompress(byte[] encrypted)
        {
            var json = Compressor.Decompress(encrypted, out CompressionOptions option);
            var amounts = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json, _serializerOptions);
            return new EncryptedData<Dictionary<string, decimal>>(amounts, option);
        }
    }
}
