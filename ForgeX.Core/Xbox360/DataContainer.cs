namespace ForgeX.Core.Xbox360;

/// <summary>
/// Holds a block of data and its offset within the STFS container.
/// </summary>
public class DataContainer
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public long Offset { get; set; }
}
