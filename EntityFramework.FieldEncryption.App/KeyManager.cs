using EntityFramework.FieldEncryption.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EntityFramework.FieldEncryption
{
    public sealed class KeyManager : IHostedService
    {
        private readonly IKeyStore _keyStore;
        private readonly KeyManagementSettings _settings;
        private readonly ILogger<KeyManager> _logger;
        private Task _refreshTask;
        private CancellationTokenSource _cancellationTokenSource;
        private KeyChain _keyChain;
        private readonly TimeSpan _expiringThreshold;
        private readonly TimeSpan _refreshUsageThreshold;
        private readonly AppDbContext _context;

        public KeyManager(IKeyStore keyStore,
            KeyManagementSettings settings,
            AppDbContext context,
            ILogger<KeyManager> logger)
        {
            _keyStore = keyStore;
            _settings = settings;
            _context = context;
            _logger = logger;
            _expiringThreshold = _settings.TimetoLive / 10;
            _refreshUsageThreshold = _expiringThreshold * 2;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await SeedTestKeysAsync();
            KeyChain.Initialise(_keyStore,
                _settings.TimetoLive,
                _settings.DurationOfCircuitBreak,
                _settings.FailureThreshold);
            _keyChain = KeyChain.Instance;
            _cancellationTokenSource = new CancellationTokenSource();
            _refreshTask = StartRefreshAsync();
        }

        private async Task SeedTestKeysAsync()
        {
            var keyCount = 2;
            var keys = await _context.DataEncryptionKeys.ToListAsync();
            if (keys.Count > 0)
            {
                return;
            }
            for (int i = 0; i < keyCount; i++)
            {
                var keyEncryptionKeyId = Guid.NewGuid();
                _keyStore.GenerateKey(keyEncryptionKeyId);
            }
        }

        private async Task StartRefreshAsync()
        {
            _logger.LogDebug("Starting Key Refresh task");
            var period = TimeSpan.FromSeconds(10);
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(period, _cancellationTokenSource.Token);
                    RefreshKeys();
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
            _logger.LogDebug("Finishing Key Refresh task");
        }

        private void RefreshKeys()
        {
            var keys = _keyChain.GetKeys();
            foreach (var key in keys)
            {
                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }
                RefreshKey(key);
            }
        }

        private void RefreshKey(Key key)
        {
            try
            {
                if (KeyInUseIsExpiring(key))
                {
                    _logger.LogDebug("Refreshing key: {dataEncryptionKeyId}", key.DataEncryptionKeyId);
                    key.Refresh(_keyStore, _settings.TimetoLive, true);
                    _logger.LogDebug("Finished refreshing key: {dataEncryptionKeyId}", key.DataEncryptionKeyId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing key: {dataEncryptionKeyId}", key.DataEncryptionKeyId);
            }
        }

        private bool KeyInUseIsExpiring(Key key)
        {
            return KeyIsExpiring(key) && KeyIsInUse(key);
        }

        private bool KeyIsInUse(Key key)
        {
            return (DateTime.UtcNow - _refreshUsageThreshold) < key.LastOperationDate;
        }

        private bool KeyIsExpiring(Key key)
        {
            return key.ExpiryDate < DateTime.UtcNow.Add(_expiringThreshold);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_refreshTask == null)
            {
                return;
            }

            _cancellationTokenSource.Cancel();
            await Task.WhenAny(_refreshTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }
}
