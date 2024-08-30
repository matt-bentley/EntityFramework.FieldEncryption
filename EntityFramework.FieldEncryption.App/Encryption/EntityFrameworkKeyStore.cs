using EntityFramework.FieldEncryption.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;

namespace EntityFramework.FieldEncryption.Encryption
{
    public sealed class EntityFrameworkKeyStore : IKeyStore
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IKeyWrapper _keyWrapper;

        public EntityFrameworkKeyStore(IServiceProvider serviceProvider,
            IKeyWrapper keyWrapper)
        {
            _serviceProvider = serviceProvider;
            _keyWrapper = keyWrapper;
        }

        public byte[] GetKey(Guid dataEncryptionKeyId)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                using (var context = scope.ServiceProvider.GetRequiredService<AppDbContext>())
                {
                    var dataEncryptionKey = context.DataEncryptionKeys.Find(dataEncryptionKeyId);
                    if (dataEncryptionKey == null)
                    {
                        throw new InvalidOperationException("Data Encryption Key not found");
                    }
                    var wrappedKey = Convert.FromBase64String(dataEncryptionKey.WrappedKey);
                    return _keyWrapper.UnwrapKey(wrappedKey, dataEncryptionKey.KeyEncryptionKeyId);
                }
            }
        }

        public byte[] GenerateKey(Guid keyEncryptionKeyId)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                using (var context = scope.ServiceProvider.GetRequiredService<AppDbContext>())
                {
                    var key = GenerateKey();
                    var wrappedKey = _keyWrapper.WrapKey(key, keyEncryptionKeyId);
                    var dataEncryptionKey = DataEncryptionKey.Create(keyEncryptionKeyId, Convert.ToBase64String(wrappedKey));
                    context.DataEncryptionKeys.Add(dataEncryptionKey);
                    context.SaveChanges();
                    return wrappedKey;
                }
            }
        }

        private static byte[] GenerateKey()
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.GenerateKey();
                return aes.Key;
            }
        }
    }
}
