using EntityFramework.FieldEncryption.Entities;
using Microsoft.EntityFrameworkCore;

namespace EntityFramework.FieldEncryption
{
    public sealed class AppDbContext : DbContext
    {
        public DbSet<EventStream> EventStreams { get; set; }
        public DbSet<EncryptedEventStream> EncryptedEventStreams { get; set; }
        public DbSet<FinancialEntity> FinancialEntities { get; set; }
        public DbSet<EncryptedFinancialEntity> EncryptedFinancialEntities { get; set; }
        public DbSet<DataEncryptionKey> DataEncryptionKeys { get; set; }
        public DbSet<Statistics> Statistics { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer("Server=127.0.0.1, 1433; Database=FieldEncryption; Integrated Security=False; User Id = SA; Password=Admin1234!; MultipleActiveResultSets=False;TrustServerCertificate=True");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
