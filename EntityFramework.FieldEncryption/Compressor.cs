using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace EntityFramework.FieldEncryption
{
    public static class Compressor
    {
        public static byte[] Compress(ReadOnlySpan<char> value, CompressionOptions options)
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

        public static string Decompress(byte[] value, out CompressionOptions options)
        {
            using (MemoryStream inputStream = new MemoryStream(value))
            {
                var compressionType = (CompressionType)inputStream.ReadByte();

                Span<byte> valueLength = stackalloc byte[4];
                inputStream.Read(valueLength);
                var outputLength = BitConverter.ToInt32(valueLength);

                if (compressionType == CompressionType.GZip)
                {
                    options = new CompressionOptions(CompressionType.GZip, Guid.Empty);
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
                        options = new CompressionOptions(CompressionType.GZipWithAes256, new Guid(keyIdentifier));
                        byte[] iv = new byte[16];
                        inputStream.Read(iv);
                        var key = KeyChain.Instance.GetKey(options.DataEncryptionKeyId);
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
