
namespace EntityFramework.FieldEncryption.Benchmarks
{
    public sealed class StubKeyStore : IKeyStore
    {
        private const string _keyBase64 = "e6jiwndHfcTlO9U8ZBe17n+eCuafvTLCjbNbtbQcU+E=";
        private readonly static byte[] _key = Convert.FromBase64String(_keyBase64);

        public byte[] GenerateKey(Guid keyEncryptionKeyId)
        {
            throw new NotImplementedException();
        }

        public byte[] GetKey(Guid dataEncryptionKeyId)
        {
            return _key;
        }
    }
}
