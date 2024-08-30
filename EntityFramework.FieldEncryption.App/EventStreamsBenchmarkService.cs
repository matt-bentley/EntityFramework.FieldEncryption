using EntityFramework.FieldEncryption.DomainEvents;
using EntityFramework.FieldEncryption.Encryption;
using EntityFramework.FieldEncryption.Entities;
using EntityFramework.FieldEncryption.Entities.Abstract;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace EntityFramework.FieldEncryption
{
    public sealed class BenchmarkService : BackgroundService
    {
        private readonly ILogger<BenchmarkService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly Random _randomGenerator = new Random();

        public BenchmarkService(ILogger<BenchmarkService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
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
            var eventStreamType = typeof(EventStream);
            _logger.LogInformation("Running {type} benchmark for {i} items", eventStreamType.Name, count);

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var ids = new ConcurrentQueue<Guid>();

            await Parallel.ForAsync(0, count, parallelOptions, async (i, _) =>
            {
                try
                {
                    var createdEvent = GetCreatedEvent();
                    var eventStream = EventStream.Create(createdEvent.ProductId, createdEvent);
                    var updatedEvents = GetUpdatedEvents(createdEvent.ProductId);

                    foreach(var updatedEvent in updatedEvents)
                    {
                        eventStream.AddEvent(updatedEvent);
                    }

                    using(var scope = _serviceProvider.CreateScope())
                    using (var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>())
                    {
                        dbContext.EventStreams.Add(eventStream);
                        await dbContext.SaveChangesAsync();
                        if ((i % queryBatchSize) == 0)
                        {
                            ids.Enqueue(eventStream.Id);
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
                            var eventStream = await dbContext.EventStreams.FindAsync(id);
                            var updatedEvent = GetUpdatedEvent(eventStream.Id);
                            eventStream.AddEvent(updatedEvent);
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
            eventStreamType = typeof(EncryptedEventStream);
            _logger.LogInformation("Running {type} benchmark for {i} items", eventStreamType.Name, count);

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
                    var createdEvent = GetCreatedEvent();
                    var keyIndex = i % encryptionKeys.Count;
                    var key = encryptionKeys[keyIndex];
                    var eventStream = EncryptedEventStream.Create(createdEvent.ProductId, createdEvent, new CompressionOptions(CompressionType.GZipWithAes256, key.Id));
                    var updatedEvents = GetUpdatedEvents(createdEvent.ProductId);

                    foreach (var updatedEvent in updatedEvents)
                    {
                        eventStream.AddEvent(updatedEvent);
                    }

                    using (var scope = _serviceProvider.CreateScope())
                    using (var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>())
                    {
                        dbContext.EncryptedEventStreams.Add(eventStream);
                        await dbContext.SaveChangesAsync();
                        if ((i % queryBatchSize) == 0)
                        {
                            ids.Enqueue(eventStream.Id);
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
            queryCount = ids.Count;

            await Parallel.ForAsync(0, queryCount, parallelOptions, async (i, _) =>
            {
                try
                {
                    using (var dbContext = new AppDbContext())
                    {
                        if (ids.TryDequeue(out var id))
                        {
                            var eventStream = await dbContext.EncryptedEventStreams.FindAsync(id);
                            var updatedEvent = GetUpdatedEvent(eventStream.Id);
                            eventStream.AddEvent(updatedEvent);
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


            _logger.LogInformation("Completed benchmarks.");
        }

        private ProductCreatedEvent GetCreatedEvent()
        {
            var id = Guid.NewGuid();
            return new ProductCreatedEvent
            {
                ProductId = Entity.NewId(),
                Name = id.ToString(),
                Price = GetRandomPrice()
            };
        }

        private IEnumerable<ProductUpdatedEvent> GetUpdatedEvents(Guid id)
        {
            var count = 10;
            for (int i = 0; i < count; i++)
            {
                yield return GetUpdatedEvent(id);
            }
        }

        private ProductUpdatedEvent GetUpdatedEvent(Guid id)
        {
            var name = id.ToString();
            return new ProductUpdatedEvent
            {
                ProductId = id,
                Name = name,
                Price = GetRandomPrice()
            };
        }

        private decimal GetRandomPrice()
        {
            var price = (decimal)_randomGenerator.Next(0, 1000);
            price += (decimal)_randomGenerator.NextDouble();
            return price;
        }
    }
}
