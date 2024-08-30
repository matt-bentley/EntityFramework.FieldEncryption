using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using EntityFramework.FieldEncryption.Entities;

namespace EntityFramework.FieldEncryption.Configurations
{
    public sealed class DataEncryptionKeyConfiguration : IEntityTypeConfiguration<DataEncryptionKey>
    {
        public void Configure(EntityTypeBuilder<DataEncryptionKey> builder)
        {
            builder.Property(e => e.Id)
                   .ValueGeneratedNever();

            builder.Property(e => e.WrappedKey)
                  .HasColumnType("varchar(512)")
                  .IsRequired();
        }
    }
}
