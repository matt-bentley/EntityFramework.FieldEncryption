using EntityFramework.FieldEncryption.Encryption;
using EntityFramework.FieldEncryption.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace EntityFramework.FieldEncryption
{
    public sealed class FinancialEntitiesBenchmarkService : BackgroundService
    {
        private readonly ILogger<FinancialEntitiesBenchmarkService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly List<KeyValuePair<string, decimal>> _amounts = GetAmounts();

        public FinancialEntitiesBenchmarkService(ILogger<FinancialEntitiesBenchmarkService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        private static List<KeyValuePair<string, decimal>> GetAmounts()
        {
            return [
                new KeyValuePair<string, decimal>("Revenue", 500_000m),
                new KeyValuePair<string, decimal>("Expenses", 400_000m),
                new KeyValuePair<string, decimal>("ProfitBeforeTax", 100_000m),
                new KeyValuePair<string, decimal>("Tax", 20_000m),
                new KeyValuePair<string, decimal>("ProfitAfterTax", 80_000m),
            ];
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting benchmarks..");

            int count = 100_000;
            int queryBatchSize = 5;
            int maxConcurrency = 12;
            int logBatchSize = 10_000;
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxConcurrency
            };

            // start benchmark
            var eventStreamType = typeof(FinancialEntity);
            _logger.LogInformation("Running {type} benchmark for {i} items", eventStreamType.Name, count);

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var ids = new ConcurrentQueue<Guid>();

            await Parallel.ForAsync(0, count, parallelOptions, async (i, _) =>
            {
                try
                {
                    var entity = FinancialEntity.Create($"Entity {i}", $"X000{i}", DateTime.UtcNow);
                    foreach(var amount in _amounts)
                    {
                        entity.SetAmount(amount.Key, amount.Value);
                    }

                    using(var scope = _serviceProvider.CreateScope())
                    using (var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>())
                    {
                        dbContext.FinancialEntities.Add(entity);
                        await dbContext.SaveChangesAsync();
                        if ((i % queryBatchSize) == 0)
                        {
                            ids.Enqueue(entity.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error inserting {type} {i}", i, eventStreamType.Name);
                }
                if (i % logBatchSize == 0)
                {
                    _logger.LogInformation("Inserted {i} {type}s", i, eventStreamType.Name);
                }
            });

            _logger.LogInformation("Inserted {count} {type} in: {elapsed}s", count, eventStreamType.Name, stopwatch.Elapsed.TotalSeconds);
            using (var scope = _serviceProvider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>())
            {
                dbContext.Statistics.Add(new Statistics(eventStreamType.Name, "Insert", count, stopwatch.Elapsed.TotalSeconds));
                await dbContext.SaveChangesAsync();
            }
            stopwatch.Restart();
            var queryCount = ids.Count;

            await Parallel.ForAsync(0, queryCount, parallelOptions, async (i, _) =>
            {
                try
                {
                    using (var dbContext = new AppDbContext())
                    {
                        if (ids.TryDequeue(out var id))
                        {
                            var entity = await dbContext.FinancialEntities.FindAsync(id);
                            entity.SetAmount("Profit", 80_000m);
                            await dbContext.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating {type} {i}", i, eventStreamType.Name);
                }
                if (i % logBatchSize == 0)
                {
                    _logger.LogInformation("Updated {i} {type}s", i, eventStreamType.Name);
                }
            });

            stopwatch.Stop();
            _logger.LogInformation("Updated {count} {type} in: {elapsed}s", queryCount, eventStreamType.Name, stopwatch.Elapsed.TotalSeconds);
            using (var scope = _serviceProvider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>())
            {
                dbContext.Statistics.Add(new Statistics(eventStreamType.Name, "Update", queryCount, stopwatch.Elapsed.TotalSeconds));
                await dbContext.SaveChangesAsync();
            }
            stopwatch.Reset();
            ids.Clear();


            // start benchmark
            eventStreamType = typeof(EncryptedFinancialEntity);
            var compressionType = CompressionType.GZipWithAes256;
            _logger.LogInformation("Running {type}:{compressionType} benchmark for {i} items", eventStreamType.Name, compressionType, count);

            stopwatch.Restart();

            ids = new ConcurrentQueue<Guid>();
            
            List<DataEncryptionKey> encryptionKeys = new List<DataEncryptionKey>();
            using(var dbContext = _serviceProvider.GetRequiredService<AppDbContext>())
            {
                encryptionKeys = await dbContext.DataEncryptionKeys.ToListAsync();
            }

            await Parallel.ForAsync(0, count, parallelOptions, async (i, _) =>
            {
                try
                {
                    var keyIndex = i % encryptionKeys.Count;
                    var key = encryptionKeys[keyIndex];

                    var entity = EncryptedFinancialEntity.Create($"Entity {i}", $"X000{i}", DateTime.UtcNow, new CompressionOptions(compressionType, key.Id));
                    foreach (var amount in _amounts)
                    {
                        entity.SetAmount(amount.Key, amount.Value);
                    }

                    using (var scope = _serviceProvider.CreateScope())
                    using (var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>())
                    {
                        dbContext.EncryptedFinancialEntities.Add(entity);
                        await dbContext.SaveChangesAsync();
                        if ((i % queryBatchSize) == 0)
                        {
                            ids.Enqueue(entity.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error inserting {type} {i}", i, eventStreamType.Name);
                }
                if (i % logBatchSize == 0)
                {
                    _logger.LogInformation("Inserted {i} {type}s", i, eventStreamType.Name);
                }
            });

            _logger.LogInformation("Inserted {count} {type} in: {elapsed}s", count, eventStreamType.Name, stopwatch.Elapsed.TotalSeconds);
            using (var scope = _serviceProvider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>())
            {
                dbContext.Statistics.Add(new Statistics($"{eventStreamType.Name}:{compressionType}", "Insert", count, stopwatch.Elapsed.TotalSeconds));
                await dbContext.SaveChangesAsync();
            }
            stopwatch.Restart();
            queryCount = ids.Count;

            await Parallel.ForAsync(0, queryCount, parallelOptions, async (i, _) =>
            {
                try
                {
                    using (var dbContext = new AppDbContext())
                    {
                        if (ids.TryDequeue(out var id))
                        {
                            var entity = await dbContext.EncryptedFinancialEntities.FindAsync(id);
                            entity.SetAmount("Profit", 80_000m);
                            await dbContext.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating {type} {i}", i, eventStreamType.Name);
                }
                if (i % logBatchSize == 0)
                {
                    _logger.LogInformation("Updated {i} {type}s", i, eventStreamType.Name);
                }
            });

            stopwatch.Stop();
            _logger.LogInformation("Updated {count}:{compressionType} {type} in: {elapsed}s", queryCount, eventStreamType.Name, compressionType, stopwatch.Elapsed.TotalSeconds);
            using (var scope = _serviceProvider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>())
            {
                dbContext.Statistics.Add(new Statistics($"{eventStreamType.Name}:{compressionType}", "Update", queryCount, stopwatch.Elapsed.TotalSeconds));
                await dbContext.SaveChangesAsync();
            }
            stopwatch.Reset();
            ids.Clear();


            // start benchmark
            eventStreamType = typeof(EncryptedFinancialEntity);
            compressionType = CompressionType.GZip;
            _logger.LogInformation("Running {type}:{compressionType} benchmark for {i} items", eventStreamType.Name, compressionType, count);

            stopwatch.Restart();

            ids = new ConcurrentQueue<Guid>();

            await Parallel.ForAsync(0, count, parallelOptions, async (i, _) =>
            {
                try
                {
                    var keyIndex = i % encryptionKeys.Count;
                    var key = encryptionKeys[keyIndex];

                    var entity = EncryptedFinancialEntity.Create($"Entity {i}", $"X000{i}", DateTime.UtcNow, new CompressionOptions(compressionType, key.Id));
                    foreach (var amount in _amounts)
                    {
                        entity.SetAmount(amount.Key, amount.Value);
                    }

                    using (var scope = _serviceProvider.CreateScope())
                    using (var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>())
                    {
                        dbContext.EncryptedFinancialEntities.Add(entity);
                        await dbContext.SaveChangesAsync();
                        if ((i % queryBatchSize) == 0)
                        {
                            ids.Enqueue(entity.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error inserting {type} {i}", i, eventStreamType.Name);
                }
                if (i % logBatchSize == 0)
                {
                    _logger.LogInformation("Inserted {i} {type}s", i, eventStreamType.Name);
                }
            });

            _logger.LogInformation("Inserted {count} {type} in: {elapsed}s", count, eventStreamType.Name, stopwatch.Elapsed.TotalSeconds);
            using (var scope = _serviceProvider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>())
            {
                dbContext.Statistics.Add(new Statistics($"{eventStreamType.Name}:{compressionType}", "Insert", count, stopwatch.Elapsed.TotalSeconds));
                await dbContext.SaveChangesAsync();
            }
            stopwatch.Restart();
            queryCount = ids.Count;

            await Parallel.ForAsync(0, queryCount, parallelOptions, async (i, _) =>
            {
                try
                {
                    using (var dbContext = new AppDbContext())
                    {
                        if (ids.TryDequeue(out var id))
                        {
                            var entity = await dbContext.EncryptedFinancialEntities.FindAsync(id);
                            entity.SetAmount("Profit", 80_000m);
                            await dbContext.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating {type} {i}", i, eventStreamType.Name);
                }
                if (i % logBatchSize == 0)
                {
                    _logger.LogInformation("Updated {i} {type}s", i, eventStreamType.Name);
                }
            });

            stopwatch.Stop();
            _logger.LogInformation("Updated {count}:{compressionType} {type} in: {elapsed}s", queryCount, eventStreamType.Name, compressionType, stopwatch.Elapsed.TotalSeconds);
            using (var scope = _serviceProvider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>())
            {
                dbContext.Statistics.Add(new Statistics($"{eventStreamType.Name}:{compressionType}", "Update", queryCount, stopwatch.Elapsed.TotalSeconds));
                await dbContext.SaveChangesAsync();
            }
            stopwatch.Reset();
            ids.Clear();


            _logger.LogInformation("Completed benchmarks.");
        }
    }
}
