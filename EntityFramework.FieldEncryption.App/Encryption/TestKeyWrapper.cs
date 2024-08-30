using System.Security.Cryptography;

namespace EntityFramework.FieldEncryption.Encryption
{
    public sealed class TestKeyWrapper : IKeyWrapper
    {
        private static readonly byte[] _iv = Convert.FromBase64String("CaIb79joIXP0v9RaKHZRjg==");

        public byte[] WrapKey(byte[] unwrappedKey, Guid keyEncryptionKeyId)
        {
            using (var aes = CreateAes(keyEncryptionKeyId))
            using (var outputStream = new MemoryStream())
            {
                using (var cryptoStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cryptoStream.Write(unwrappedKey);
                }
                return outputStream.ToArray();
            }
        }

        public byte[] UnwrapKey(byte[] wrappedKey, Guid keyEncryptionKeyId)
        {
            using (var aes = CreateAes(keyEncryptionKeyId))
            {
                using (MemoryStream msDecrypt = new MemoryStream(wrappedKey))
                {
                    using (var decryptor = aes.CreateDecryptor())
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (var msOutput = new MemoryStream())
                    {
                        csDecrypt.CopyTo(msOutput);
                        return msOutput.ToArray();
                    }
                }
            }
        }

        private static Aes CreateAes(Guid keyEncryptionKeyId)
        {
            var aes = Aes.Create();
            aes.Key = keyEncryptionKeyId.ToByteArray();
            aes.IV = _iv;
            return aes;
        }
    }
}
