using EntityFramework.FieldEncryption.Encryption;

namespace EntityFramework.FieldEncryption.Entities
{
    public sealed class EncryptedData<T>
    {
        public EncryptedData(T data, CompressionOptions options)
        {
            Data = data;
            Options = options;
        }

        public T Data { get; private set; }
        public CompressionOptions Options { get; private set; }

        public void Update(T data)
        {
            Data = data;
        }
    }
}
