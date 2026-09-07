using System.IO;
using System.Text;
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
}