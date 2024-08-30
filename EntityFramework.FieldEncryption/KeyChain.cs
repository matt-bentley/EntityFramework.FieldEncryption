using System.Collections.Concurrent;

namespace EntityFramework.FieldEncryption
{
    public sealed class KeyChain
    {
        private static KeyChain _instance;
        private readonly IKeyStore _keyStore;
        private readonly TimeSpan _timeToLive;
        private readonly int _failureThreshold;
        private readonly TimeSpan _durationOfCircuitBreak;
        private ConcurrentDictionary<Guid, Key> _keys = new ConcurrentDictionary<Guid, Key>();
        private readonly object _lock = new object();
        public static bool Initialised => _instance != null;

        private KeyChain(IKeyStore keyStore,
            TimeSpan timeToLive,
            TimeSpan durationOfCircuitBreak,
            int failureThreshold)
        {
            _keyStore = keyStore;
            _timeToLive = timeToLive;
            _failureThreshold = failureThreshold;
            _durationOfCircuitBreak = durationOfCircuitBreak;
        }

        public static void Initialise(IKeyStore keyStore,
            TimeSpan timeToLive,
            TimeSpan durationOfCircuitBreak,
            int failureThreshold)
        {
            if (Initialised)
            {
                throw new InvalidOperationException("KeyChain has already been initialised");
            }
            _instance = new KeyChain(keyStore, timeToLive, durationOfCircuitBreak, failureThreshold);
        }

        public static KeyChain Instance
        {
            get
            {
                if (!Initialised)
                {
                    throw new InvalidOperationException("KeyChain has not been initialised");
                }
                return _instance;
            }
        }

        public byte[] GetKey(Guid dataEncryptionKeyId)
        {
            var key = GetKeyMetadata(dataEncryptionKeyId);
            if (key.Expired)
            {
                key.Refresh(_keyStore, _timeToLive);
            }
            key.UpdateLastOperationDate();

            return key.Value;
        }

        public IReadOnlyCollection<Key> GetKeys() => _keys.Values.ToList();

        public void ClearKeys()
        {
            _keys = new ConcurrentDictionary<Guid, Key>();
        }

        private Key GetKeyMetadata(Guid dataEncryptionKeyId)
        {
            if (!_keys.TryGetValue(dataEncryptionKeyId, out var key))
            {
                lock (_lock)
                {
                    if (!_keys.TryGetValue(dataEncryptionKeyId, out var addKey))
                    {
                        key = new Key(dataEncryptionKeyId, _failureThreshold, _durationOfCircuitBreak);
                        _keys.TryAdd(dataEncryptionKeyId, key);
                    }
                    else
                    {
                        key = addKey;
                    }
                }
            }
            return key;
        }
    }
}
