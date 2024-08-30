using EntityFramework.FieldEncryption.Encryption;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EntityFramework.FieldEncryption
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    var keyManagementSettings = new KeyManagementSettings();
                    hostContext.Configuration.GetSection("KeyManagement").Bind(keyManagementSettings);
                    services.AddSingleton(keyManagementSettings);
                    services.AddSingleton<IKeyWrapper, TestKeyWrapper>();
                    services.AddSingleton<IKeyStore, EntityFrameworkKeyStore>();
                    services.AddHostedService<KeyManager>();

                    services.AddDbContext<AppDbContext>();

                    services.AddHostedService<FinancialEntitiesBenchmarkService>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                })
                .Build();

            await host.RunAsync();
        }
    }
}
