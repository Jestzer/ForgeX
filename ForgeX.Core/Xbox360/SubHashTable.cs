namespace ForgeX.Core.Xbox360;

public class SubHashTable
{
    public int Offset { get; set; }
    public List<HashEntry> HashEntries { get; set; } = new();
    public bool IsValid { get; set; }
    public byte[] SHA1 { get; set; }

    public SubHashTable(int offset, byte[] sha1)
    {
        Offset = offset;
        SHA1 = sha1;
    }
}
