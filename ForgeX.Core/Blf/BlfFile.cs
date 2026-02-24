using System.IO.Compression;
using System.Text;

namespace ForgeX.Core.Blf;

/// <summary>
/// Reads and writes BLF (Blam File Format) chunk-based container files.
/// Used by Halo 3 MCC for .mvar usermap files.
/// </summary>
public class BlfFile
{
    public List<BlfChunk> Chunks { get; } = new();
    public string FilePath { get; private set; } = "";

    /// <summary>
    /// The format detected from the chunk tags.
    /// </summary>
    public BlfVariantFormat VariantFormat { get; private set; }

    public BlfFile(string filePath)
    {
        FilePath = filePath;
        using var stream = File.OpenRead(filePath);
        Parse(stream);
    }

    public BlfFile(byte[] data)
    {
        using var stream = new MemoryStream(data);
        Parse(stream);
    }

    private void Parse(Stream stream)
    {
        using var reader = new BinaryReader(stream);

        while (stream.Position < stream.Length)
        {
            if (stream.Length - stream.Position < 12)
                break;

            var chunk = new BlfChunk();

            // Read 4-byte tag as ASCII (big-endian order)
            byte[] tagBytes = reader.ReadBytes(4);
            chunk.Tag = Encoding.ASCII.GetString(tagBytes);

            // Size is big-endian
            chunk.Size = ReadBigEndianInt32(reader);

            // Version is big-endian
            chunk.MajorVersion = ReadBigEndianInt16(reader);
            chunk.MinorVersion = ReadBigEndianInt16(reader);

            // Read payload
            int dataSize = chunk.Size - 12;
            if (dataSize > 0)
                chunk.Data = reader.ReadBytes(dataSize);
            else
                chunk.Data = Array.Empty<byte>();

            Chunks.Add(chunk);

            // Stop after _eof chunk
            if (chunk.Tag == "_eof")
                break;
        }

        // Validate _blf header
        if (Chunks.Count == 0 || Chunks[0].Tag != "_blf")
            throw new InvalidDataException("Not a valid BLF file: missing _blf header chunk.");

        // Detect format
        if (HasChunk("mvar"))
            VariantFormat = BlfVariantFormat.PackedMvar;
        else if (HasChunk("mapv"))
            VariantFormat = BlfVariantFormat.UnpackedMapv;
        else if (HasChunk("_cmp"))
            VariantFormat = BlfVariantFormat.Compressed;
        else
            VariantFormat = BlfVariantFormat.Unknown;
    }

    public BlfChunk? GetChunk(string tag)
    {
        for (int i = 0; i < Chunks.Count; i++)
        {
            if (Chunks[i].Tag == tag)
                return Chunks[i];
        }
        return null;
    }

    public bool HasChunk(string tag)
    {
        return GetChunk(tag) != null;
    }

    /// <summary>
    /// Returns the major version of the mvar chunk, or -1 if not present.
    /// Used to distinguish Halo 3 (12) from Reach (31).
    /// </summary>
    public short GetMvarMajorVersion()
    {
        var mvar = GetChunk("mvar");
        return mvar?.MajorVersion ?? -1;
    }

    /// <summary>
    /// Writes all chunks back to the original file path.
    /// </summary>
    public void Write()
    {
        Write(FilePath);
    }

    /// <summary>
    /// Writes all chunks to the specified file path.
    /// </summary>
    public void Write(string filePath)
    {
        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream);

        foreach (var chunk in Chunks)
        {
            // Write tag
            byte[] tagBytes = Encoding.ASCII.GetBytes(chunk.Tag.PadRight(4).Substring(0, 4));
            writer.Write(tagBytes);

            // Size (big-endian)
            WriteBigEndianInt32(writer, chunk.Size);

            // Version (big-endian)
            WriteBigEndianInt16(writer, chunk.MajorVersion);
            WriteBigEndianInt16(writer, chunk.MinorVersion);

            // Payload
            if (chunk.Data.Length > 0)
                writer.Write(chunk.Data);
        }
    }

    /// <summary>
    /// Replaces the data of an existing chunk (recalculates size).
    /// </summary>
    public void UpdateChunkData(string tag, byte[] newData)
    {
        var chunk = GetChunk(tag)
            ?? throw new InvalidOperationException($"Chunk '{tag}' not found.");
        chunk.Data = newData;
        chunk.Size = newData.Length + 12;
    }

    /// <summary>
    /// Decompresses a compressed (_cmp) .mvar file and saves the decompressed copy.
    /// Returns the path to the decompressed file.
    /// </summary>
    public static string Decompress(string compressedFilePath)
    {
        var blf = new BlfFile(compressedFilePath);
        if (blf.VariantFormat != BlfVariantFormat.Compressed)
            throw new InvalidOperationException("File is not a compressed .mvar file.");

        var cmpChunk = blf.GetChunk("_cmp")
            ?? throw new InvalidDataException("Missing _cmp chunk in compressed .mvar file.");

        // The _cmp payload has a 5-byte header before the zlib-compressed data
        if (cmpChunk.Data.Length <= 5)
            throw new InvalidDataException("_cmp chunk payload too small.");

        byte[] compressedData = cmpChunk.Data[5..];

        // Decompress using zlib (standard in .NET 6+)
        byte[] decompressedData;
        using (var compressedStream = new MemoryStream(compressedData))
        using (var zlibStream = new ZLibStream(compressedStream, CompressionMode.Decompress))
        using (var resultStream = new MemoryStream())
        {
            zlibStream.CopyTo(resultStream);
            decompressedData = resultStream.ToArray();
        }

        // The decompressed data is a raw mapv chunk (tag + size + version + payload).
        // Reconstruct a valid BLF file: keep _blf and other pre-_cmp chunks,
        // replace _cmp with the decompressed mapv chunk, then write _eof.
        string decompressedTag = Encoding.ASCII.GetString(decompressedData, 0, 4);
        if (decompressedTag != "mapv")
            throw new InvalidDataException($"Expected decompressed data to be a mapv chunk, got '{decompressedTag}'.");

        // Parse the decompressed mapv chunk header
        var mapvChunk = new BlfChunk();
        mapvChunk.Tag = "mapv";
        mapvChunk.Size = ReadBigEndianInt32(decompressedData, 4);
        mapvChunk.MajorVersion = ReadBigEndianInt16(decompressedData, 8);
        mapvChunk.MinorVersion = ReadBigEndianInt16(decompressedData, 10);
        mapvChunk.Data = decompressedData[12..mapvChunk.Size];

        // Build output path: same directory, append _decompressed before extension
        string dir = Path.GetDirectoryName(compressedFilePath) ?? ".";
        string name = Path.GetFileNameWithoutExtension(compressedFilePath);
        string ext = Path.GetExtension(compressedFilePath);
        string decompressedPath = Path.Combine(dir, $"{name}_decompressed{ext}");

        // Write reconstructed BLF: all chunks except _cmp replaced with mapv
        using (var stream = File.Create(decompressedPath))
        using (var writer = new BinaryWriter(stream))
        {
            foreach (var chunk in blf.Chunks)
            {
                if (chunk.Tag == "_cmp")
                {
                    // Write decompressed mapv chunk instead
                    writer.Write(Encoding.ASCII.GetBytes("mapv"));
                    WriteBigEndianInt32(writer, mapvChunk.Size);
                    WriteBigEndianInt16(writer, mapvChunk.MajorVersion);
                    WriteBigEndianInt16(writer, mapvChunk.MinorVersion);
                    writer.Write(mapvChunk.Data);
                }
                else
                {
                    // Write original chunk as-is
                    writer.Write(Encoding.ASCII.GetBytes(chunk.Tag.PadRight(4)[..4]));
                    WriteBigEndianInt32(writer, chunk.Size);
                    WriteBigEndianInt16(writer, chunk.MajorVersion);
                    WriteBigEndianInt16(writer, chunk.MinorVersion);
                    if (chunk.Data.Length > 0)
                        writer.Write(chunk.Data);
                }
            }
        }

        return decompressedPath;
    }

    private static int ReadBigEndianInt32(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    private static int ReadBigEndianInt32(byte[] data, int offset)
    {
        byte[] bytes = new byte[4];
        Array.Copy(data, offset, bytes, 0, 4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    private static short ReadBigEndianInt16(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(2);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToInt16(bytes, 0);
    }

    private static short ReadBigEndianInt16(byte[] data, int offset)
    {
        byte[] bytes = new byte[2];
        Array.Copy(data, offset, bytes, 0, 2);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToInt16(bytes, 0);
    }

    private static void WriteBigEndianInt32(BinaryWriter writer, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        writer.Write(bytes);
    }

    private static void WriteBigEndianInt16(BinaryWriter writer, short value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        writer.Write(bytes);
    }
}

public enum BlfVariantFormat
{
    Unknown,
    PackedMvar,
    UnpackedMapv,
    Compressed
}
