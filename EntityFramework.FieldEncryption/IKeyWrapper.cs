
namespace EntityFramework.FieldEncryption
{
    public interface IKeyWrapper
    {
        byte[] WrapKey(byte[] unwrappedKey, Guid keyEncryptionKeyId);
        byte[] UnwrapKey(byte[] wrappedKey, Guid keyEncryptionKeyId);
    }
}
