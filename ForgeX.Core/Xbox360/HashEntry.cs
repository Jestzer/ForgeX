namespace ForgeX.Core.Xbox360;

public class HashEntry
{
    public byte[] SHA1 { get; set; }
    public string SHA1String { get; set; }
    public int Offset { get; set; }
    public int EntryID { get; set; }
    public long Validates { get; set; }
    public bool IsValid { get; set; }

    public HashEntry(byte[] sha1, int offset, int entryId, long validates)
    {
        SHA1 = sha1;
        SHA1String = StfsSigning.BytesToHexString(sha1);
        Offset = offset;
        EntryID = entryId;
        Validates = validates;
    }
}
