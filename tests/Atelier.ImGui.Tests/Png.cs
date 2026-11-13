using System.Buffers.Binary;
using System.IO.Compression;

namespace AtelierTests;

// Minimal opaque PNG encoder, so captures need no platform imaging API.
internal static class Png
{
    private static readonly uint[] Table = CreateTable();

    /// <summary>Writes 0xRRGGBB pixels (the top byte is ignored), first row at the top.</summary>
    public static void Save(string path, int width, int height, ReadOnlySpan<int> pixels)
    {
        if (width <= 0 || height <= 0 || pixels.Length != width * height) throw new ArgumentException("Pixel buffer does not match the image size.");
        var rows = new byte[height * (width * 3 + 1)];
        var offset = 0;
        for (var y = 0; y < height; y++)
        {
            rows[offset++] = 0; // filter: none
            for (var x = 0; x < width; x++)
            {
                var pixel = pixels[y * width + x];
                rows[offset++] = (byte)(pixel >> 16);
                rows[offset++] = (byte)(pixel >> 8);
                rows[offset++] = (byte)pixel;
            }
        }
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(rows);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // bits per channel
        header[9] = 2; // truecolor

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var file = File.Create(path);
        file.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(file, "IHDR"u8, header);
        Chunk(file, "IDAT"u8, compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        Chunk(file, "IEND"u8, []);
    }

    private static void Chunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        stream.Write(word);
        stream.Write(type);
        stream.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(word, ~Crc(data, Crc(type, uint.MaxValue)));
        stream.Write(word);
    }

    private static uint Crc(ReadOnlySpan<byte> data, uint crc)
    {
        foreach (var value in data) crc = Table[(crc ^ value) & 0xFF] ^ crc >> 8;
        return crc;
    }

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ c >> 1 : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
