
namespace EntityFramework.FieldEncryption
{
    public sealed record class CompressionOptions(CompressionType Type, Guid DataEncryptionKeyId);
}
