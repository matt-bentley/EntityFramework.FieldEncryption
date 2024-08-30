using BenchmarkDotNet.Attributes;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace EntityFramework.FieldEncryption.Benchmarks
{
    [MemoryDiagnoser]
    public class Benchmarks
    {
        private const string _json = @"{""Revenue"":500000,""Expenses"":400000,""ProfitBeforeTax"":100000,""Tax"":20000,""ProfitAfterTax"":80000,""Profit"":80000}";
        private readonly static Guid _dataEncryptionKeyId = Guid.NewGuid();
        private readonly static CompressionOptions _compressionOptions = new CompressionOptions(CompressionType.GZipWithAes256, _dataEncryptionKeyId);
        private readonly static byte[] _cipherData;

        static Benchmarks()
        {
            KeyChain.Initialise(new StubKeyStore(), TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(5), 3);
            _cipherData = CompressV1(_json, _compressionOptions);
        }

        [Benchmark]
        public byte[] CompressionV1()
        {
            return CompressV1(_json, _compressionOptions);
        }

        [Benchmark]
        public byte[] CompressionV2()
        {
            return CompressV2(_json, _compressionOptions);
        }

        [Benchmark]
        public string DecompressionV1()
        {
            return DecompressV1(_cipherData);
        }

        [Benchmark]
        public string DecompressionV2()
        {
            return DecompressV2(_cipherData);
        }

        public static byte[] CompressV1(string value, CompressionOptions options)
        {
            using (var outputStream = new MemoryStream())
            {
                var byteCount = Encoding.UTF8.GetByteCount(value);
                outputStream.WriteByte((byte)options.Type);

                byte[] valueLength = BitConverter.GetBytes(byteCount); // byte[] heap allocation
                outputStream.Write(valueLength);

                if (options.Type == CompressionType.GZip)
                {
                    using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
                    using (var writer = new StreamWriter(gzipStream))
                    {
                        writer.Write(value); // Stream Writer is slow and memory inefficient
                    }
                }
                else
                {
                    using (var aes = Aes.Create())
                    {
                        byte[] key = KeyChain.Instance.GetKey(options.DataEncryptionKeyId);
                        aes.Key = key;
                        aes.GenerateIV();
                        byte[] iv = aes.IV;

                        byte[] keyIdentifier = options.DataEncryptionKeyId.ToByteArray(); // byte[] heap allocation
                        outputStream.Write(keyIdentifier);
                        outputStream.Write(iv);

                        using (var cryptoStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                        using (var gzipStream = new GZipStream(cryptoStream, CompressionMode.Compress))
                        using (var writer = new StreamWriter(gzipStream))
                        {
                            writer.Write(value); // Stream Writer is slow and memory inefficient
                        }
                    }
                }

                return outputStream.ToArray();
            }
        }

        public static byte[] CompressV2(string value, CompressionOptions options)
        {
            var encoder = new UTF8Encoding(false);
            int byteCount = encoder.GetByteCount(value);
            var estimatedZippedLength = byteCount / 10;
            using (var outputStream = new MemoryStream(estimatedZippedLength))
            {
                outputStream.WriteByte((byte)options.Type);

                Span<byte> valueLength = stackalloc byte[4];
                BitConverter.TryWriteBytes(valueLength, byteCount);
                outputStream.Write(valueLength);

                if (options.Type == CompressionType.GZip)
                {
                    using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
                    {
                        WriteStringToStream(value, gzipStream, encoder, byteCount);
                    }
                }
                else
                {
                    var key = KeyChain.Instance.GetKey(options.DataEncryptionKeyId);
                    Span<byte> keyIdentifier = stackalloc byte[16];
                    options.DataEncryptionKeyId.TryWriteBytes(keyIdentifier);
                    using (var aes = Aes.Create())
                    {
                        aes.Key = key;
                        aes.GenerateIV();
                        byte[] iv = aes.IV;

                        outputStream.Write(keyIdentifier);
                        outputStream.Write(iv);

                        using (var cryptoStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                        using (var gzipStream = new GZipStream(cryptoStream, CompressionMode.Compress))
                        {
                            WriteStringToStream(value, gzipStream, encoder, byteCount);
                        }
                    }
                }

                return outputStream.ToArray();
            }
        }

        private static void WriteStringToStream(ReadOnlySpan<char> value, Stream stream, UTF8Encoding encoder, int byteCount)
        {
            const int chunkSize = 8192;
            int chunkLength = chunkSize;

            if (HasNonAsciiCharacters(value.Length, byteCount))
            {
                int maxByteCount = encoder.GetMaxByteCount(1);
                chunkLength = chunkSize / maxByteCount;
            }

            int offset = 0;
            Span<byte> buffer = stackalloc byte[chunkSize];

            while (offset < value.Length)
            {
                int charsToProcess = Math.Min(value.Length - offset, chunkLength);
                int bytesWritten = encoder.GetBytes(value.Slice(offset, charsToProcess), buffer);

                if (bytesWritten == chunkSize)
                {
                    stream.Write(buffer);
                }
                else
                {
                    stream.Write(buffer.Slice(0, bytesWritten));
                }

                offset += charsToProcess;
            }
        }

        private static bool HasNonAsciiCharacters(int stringLength, int byteCount)
        {
            return stringLength != byteCount;
        }

        public static string DecompressV1(byte[] value)
        {
            using (MemoryStream inputStream = new MemoryStream(value))
            {
                var compressionType = (CompressionType)inputStream.ReadByte();
                byte[] valueLength = new byte[4];
                inputStream.Read(valueLength);
                int byteCount = BitConverter.ToInt32(valueLength);

                if (compressionType == CompressionType.GZip)
                {
                    using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
                    using (var reader = new StreamReader(gzipStream))
                    {
                        return reader.ReadToEnd();
                    }
                }
                else
                {
                    using (var aes = Aes.Create())
                    {
                        byte[] keyIdentifier = new byte[16];
                        inputStream.Read(keyIdentifier);
                        var dataEncryptionKeyId = new Guid(keyIdentifier);
                        byte[] iv = new byte[16];
                        inputStream.Read(iv);
                        var key = KeyChain.Instance.GetKey(dataEncryptionKeyId);
                        aes.Key = key;
                        aes.IV = iv;

                        using (var cryptoStream = new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read))
                        using (var gzipStream = new GZipStream(cryptoStream, CompressionMode.Decompress))
                        using (var reader = new StreamReader(gzipStream))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
        }

        public static string DecompressV2(byte[] value)
        {
            using (MemoryStream inputStream = new MemoryStream(value))
            {
                var compressionType = (CompressionType)inputStream.ReadByte();

                Span<byte> valueLength = stackalloc byte[4];
                inputStream.Read(valueLength);
                var outputLength = BitConverter.ToInt32(valueLength);

                if (compressionType == CompressionType.GZip)
                {
                    using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
                    {
                        return StreamToString(gzipStream, outputLength);
                    }
                }
                else
                {
                    using (var aes = Aes.Create())
                    {
                        Span<byte> keyIdentifier = stackalloc byte[16];
                        inputStream.Read(keyIdentifier);
                        var dataEncryptionKeyId = new Guid(keyIdentifier);
                        byte[] iv = new byte[16];
                        inputStream.Read(iv);
                        var key = KeyChain.Instance.GetKey(dataEncryptionKeyId);
                        aes.Key = key;
                        aes.IV = iv;

                        using (var cryptoStream = new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read))
                        using (var gzipStream = new GZipStream(cryptoStream, CompressionMode.Decompress))
                        {
                            return StreamToString(gzipStream, outputLength);
                        }
                    }
                }
            }
        }

        private static string StreamToString(Stream stream, int outputLength)
        {
            const int chunkSize = 8192;
            Span<byte> buffer = stackalloc byte[chunkSize];
            using (var outputStream = new MemoryStream(outputLength))
            {
                while (true)
                {
                    int bytesRead = stream.Read(buffer);
                    if (bytesRead == chunkSize)
                    {
                        outputStream.Write(buffer);
                    }
                    else if (bytesRead > 0)
                    {
                        outputStream.Write(buffer.Slice(0, bytesRead));
                    }
                    else
                    {
                        break;
                    }
                }

                return Encoding.UTF8.GetString(outputStream.ToArray());
            }
        }
    }
}
