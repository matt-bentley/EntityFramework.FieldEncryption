using EntityFramework.FieldEncryption.Entities.Abstract;

namespace EntityFramework.FieldEncryption.Entities
{
    public sealed class DataEncryptionKey : Entity
    {
        private DataEncryptionKey(Guid keyEncryptionKeyId,
            string wrappedKey,
            DateTime createdDate,
            DateTime lastRotationDate)
        {
            KeyEncryptionKeyId = keyEncryptionKeyId;
            WrappedKey = wrappedKey;
            CreatedDate = createdDate;
            LastRotationDate = lastRotationDate;
        }

        public static DataEncryptionKey Create(Guid keyEncryptionKeyId, string wrappedKey)
        {
            var now = DateTime.UtcNow;
            return new DataEncryptionKey(keyEncryptionKeyId, wrappedKey, now, now);
        }

        public Guid KeyEncryptionKeyId { get; private set; }
        public string WrappedKey { get; private set; }
        public DateTime CreatedDate { get; private set; }
        public DateTime LastRotationDate { get; private set; }

        public void Rotate(Guid keyEncryptionKeyId, string wrappedKey)
        {
            WrappedKey = wrappedKey;
            KeyEncryptionKeyId = keyEncryptionKeyId;
            LastRotationDate = DateTime.UtcNow;
        }
    }
}
