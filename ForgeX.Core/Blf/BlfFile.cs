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

    private static int ReadBigEndianInt32(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
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
