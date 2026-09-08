using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using Playnite;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.Xz;

namespace AmazonClientless;

public class Helpers
{
    private static readonly ILogger Logger = LogManager.GetLogger<Helpers>();

    public static async Task<byte[]> DecompressLzma(byte[] inputBytes)
    {
        byte[] xzHeaderMagic = [0xFD, (byte)'7', (byte)'z', (byte)'X', (byte)'Z', 0x00];

        var isXz = true;
        for (var i = 0; i < xzHeaderMagic.Length; ++i)
        {
            if (inputBytes[i] != xzHeaderMagic[i])
            {
                isXz = false;
            }
        }

        using var newInputStream = new MemoryStream(inputBytes);
        newInputStream.Seek(0, 0);


        using var outputStream = new MemoryStream();
        try
        {
            if (!isXz)
            {
                // Parse LZMA header: 5 bytes properties + 8 bytes uncompressed size
                var properties = new byte[5];
                await newInputStream.ReadExactlyAsync(properties);
                var uncompressedSize = BitConverter.ToInt64(inputBytes, 5);
                var compressedSize = newInputStream.Length - (newInputStream.Position + 8);
                await using var lzmaStream = await LzmaStream.CreateAsync(properties, newInputStream,
                    compressedSize, uncompressedSize, null, false);
                await lzmaStream.CopyToAsync(outputStream);
            }
            else
            {
                await using Stream xz = new XZStream(newInputStream);
                await xz.CopyToAsync(outputStream);
            }
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Failed to decompress response");
        }

        return outputStream.ToArray();
    }

    public static string GetSHA256(string filePath, IProgress<int>? progress = null, CancellationToken token = default)
    {
        var bufferSize = 512 * 1024;
        using var stream = new FileStream(filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: bufferSize,
            FileOptions.SequentialScan);
        using var sha256 = SHA256.Create();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            int read;
            long total = 0;

            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                sha256.TransformBlock(buffer, 0, read, null, 0);
                total += read;
                progress?.Report(read);
            }

            sha256.TransformFinalBlock([], 0, 0);
            if (sha256.Hash != null)
            {
                return Convert.ToHexStringLower(sha256.Hash);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return "";
    }
}