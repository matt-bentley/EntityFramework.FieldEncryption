using Polly;
using Polly.CircuitBreaker;

namespace EntityFramework.FieldEncryption
{
    public sealed class Key
    {
        public readonly Guid DataEncryptionKeyId;
        public DateTime LastRefreshDate { get; private set; }
        public DateTime ExpiryDate { get; private set; }

        public bool Expired => DateTime.UtcNow > ExpiryDate;
        private readonly object _lock = new object();
        private readonly CircuitBreakerPolicy _circuitBreaker;

        private byte[] _value;
        public byte[] Value
        {
            get
            {
                if (Expired)
                {
                    throw new InvalidOperationException("Key has expired");
                }
                return _value;
            }
            set { _value = value; }
        }

        private long _lastOperationTicks;
        public DateTime LastOperationDate
        {
            get
            {
                long ticks = Interlocked.Read(ref _lastOperationTicks);
                return new DateTime(ticks, DateTimeKind.Utc);
            }
            private set
            {
                Interlocked.Exchange(ref _lastOperationTicks, value.Ticks);
            }
        }

        public Key(Guid dataEncryptionKeyId, int failureThreshold, TimeSpan durationOfBreak)
        {
            DataEncryptionKeyId = dataEncryptionKeyId;
            _circuitBreaker = Policy.Handle<Exception>()
                                    .CircuitBreaker(failureThreshold, durationOfBreak);
        }

        public void UpdateLastOperationDate()
        {
            LastOperationDate = DateTime.UtcNow;
        }

        public void Refresh(IKeyStore keyStore, TimeSpan timeToLive, bool forceRefresh = false)
        {
            lock (_lock)
            {
                if (forceRefresh || Expired)
                {
                    _circuitBreaker.Execute(() =>
                    {
                        var key = keyStore.GetKey(DataEncryptionKeyId);
                        Refresh(key, DateTime.UtcNow.Add(timeToLive));
                    });
                }
            }
        }

        private void Refresh(byte[] value, DateTime expiryDate)
        {
            _value = value;
            LastRefreshDate = DateTime.UtcNow;
            ExpiryDate = expiryDate;
        }
    }
}
