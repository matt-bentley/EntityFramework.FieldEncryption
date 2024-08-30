
namespace EntityFramework.FieldEncryption
{
    public interface IKeyStore
    {
        public byte[] GetKey(Guid dataEncryptionKeyId);
        byte[] GenerateKey(Guid keyEncryptionKeyId);
    }
}
