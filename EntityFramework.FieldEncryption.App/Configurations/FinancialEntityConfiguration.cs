using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using EntityFramework.FieldEncryption.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace EntityFramework.FieldEncryption.Configurations
{
    public sealed class FinancialEntityConfiguration : IEntityTypeConfiguration<FinancialEntity>
    {
        public void Configure(EntityTypeBuilder<FinancialEntity> builder)
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

            Func<Dictionary<string, decimal>, Dictionary<string, decimal>, bool> equalityComparer = (left, right) =>
            {
                return left.Keys.Count == right.Keys.Count
                    && left.Keys.All(key => right.TryGetValue(key, out decimal value) && left[key] == value);
            };

            Func<Dictionary<string, decimal>, int> hashCodeGenerator = (amounts) =>
            {
                return amounts.GetHashCode();
            };

            var serializerOptions = new JsonSerializerOptions();

            var comparer = new ValueComparer<Dictionary<string, decimal>>(
                (l, r) => equalityComparer.Invoke(l, r),
                amounts => hashCodeGenerator.Invoke(amounts),
                v => JsonSerializer.Deserialize<Dictionary<string, decimal>>(JsonSerializer.Serialize(v, serializerOptions), serializerOptions));

            var converter = new ValueConverter<Dictionary<string, decimal>, string>(
                v => JsonSerializer.Serialize(v, serializerOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, decimal>>(v, serializerOptions));


            var eventsProperty = builder.Property(e => e.Amounts)
                                        .IsUnicode(false)
                                        .HasConversion(converter)
                                        .IsRequired();

            eventsProperty.Metadata.SetValueConverter(converter);
            eventsProperty.Metadata.SetValueComparer(comparer);
        }
    }
}
