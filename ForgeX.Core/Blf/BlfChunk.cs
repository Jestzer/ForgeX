namespace ForgeX.Core.Blf;

/// <summary>
/// Represents a single chunk in a BLF (Blam File Format) container.
/// Each chunk has a 12-byte header: tag(4) + size(4) + version_major(2) + version_minor(2).
/// </summary>
public class BlfChunk
{
    /// <summary>4-character ASCII tag identifying the chunk type (e.g. "_blf", "mvar", "mapv", "_eof").</summary>
    public string Tag { get; set; } = "";

    /// <summary>Total size of the chunk including the 12-byte header.</summary>
    public int Size { get; set; }

    public short MajorVersion { get; set; }
    public short MinorVersion { get; set; }

    /// <summary>Chunk payload (Size - 12 bytes).</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();
}
