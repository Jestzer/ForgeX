using ForgeX.Core.IO;

namespace ForgeX.Core.Xbox360;

public class MasterHashTable
{
    public List<SubHashTable> SubTables { get; set; } = new();
    public List<HashEntry>? SubTableHashes { get; set; }
    public StfsContainer Container { get; set; }
    public bool IsValid { get; set; }
    public int Offset { get; set; }

    public MasterHashTable(int offset, StfsContainer container)
    {
        Container = container;
        Offset = offset;
        ReadSubHashTables();

        foreach (var subTable in SubTables)
            ValidateSubHashTable(subTable);

        if (Container.HashCount > 170)
        {
            ReadMasterTable();
            ValidateMasterTable();
        }
        else
        {
            IsValid = SubTables[0].IsValid;
        }
    }

    public void ReadSubHashTables()
    {
        int firstOffset = Container.HashingBlock == 603979777 ? 40960 : 45056;

        Container.IO.Reader.BaseStream.Position = firstOffset;
        byte[] sha1 = StfsSigning.CalculateHash(
            Container.IO.Reader.ReadBytes(4096, EndianType.LittleEndian),
            Xbox360.HashAlgorithm.SHA1);

        SubTables.Add(new SubHashTable(firstOffset, sha1));
        Container.IO.Reader.BaseStream.Position = firstOffset;
        SubTables[0].HashEntries = Read170HashEntries(49152);

        for (int i = 1; (float)i < (float)Container.HashCount / 170f; i++)
        {
            Container.IO.Reader.BaseStream.Position = 49152 + i * 704512;
            byte[] sha = StfsSigning.CalculateHash(
                Container.IO.Reader.ReadBytes(4096, EndianType.LittleEndian),
                Xbox360.HashAlgorithm.SHA1);

            Container.IO.Reader.BaseStream.Position = 49152 + i * 704512;
            SubTables.Add(new SubHashTable(49152 + i * 704512, sha));
            SubTables[i].HashEntries = Read170HashEntries(49152 + i * 704512 + 8192);
        }
    }

    public List<HashEntry> Read170HashEntries(long startingAt)
    {
        var entries = new List<HashEntry>();
        int count = Math.Min(170, Container.HashCount);

        for (int i = 0; i < count; i++)
        {
            int offset = (int)Container.IO.Reader.BaseStream.Position;
            byte[] sha = Container.IO.Reader.ReadBytes(20, EndianType.LittleEndian);
            int entryId = Container.IO.Reader.ReadInt32();
            entries.Add(new HashEntry(sha, offset, entryId, startingAt + i * 4096));
        }
        return entries;
    }

    public void ReadMasterTable()
    {
        Container.IO.Reader.BaseStream.Position = Offset;
        SubTableHashes = new List<HashEntry>();

        for (int i = 0; (float)i < (float)Container.HashCount / 170f; i++)
        {
            int offset = (int)Container.IO.Reader.BaseStream.Position;
            byte[] sha = Container.IO.Reader.ReadBytes(20, EndianType.LittleEndian);
            int entryId = Container.IO.Reader.ReadInt32();
            SubTableHashes.Add(new HashEntry(sha, offset, entryId, 49152 + i * 704512 + 8192));
        }
    }

    public void ValidateSubHashTable(SubHashTable table)
    {
        int count = Math.Min(170, Container.HashCount);
        table.IsValid = true;

        for (int i = 0; i < count; i++)
        {
            Container.IO.Reader.BaseStream.Position = table.HashEntries[i].Validates;
            byte[] source = Container.IO.Reader.ReadBytes(4096, EndianType.LittleEndian);
            byte[] hash = StfsSigning.CalculateHash(source, Xbox360.HashAlgorithm.SHA1);
            string hashStr = StfsSigning.BytesToHexString(hash);

            if (hashStr == table.HashEntries[i].SHA1String)
            {
                table.HashEntries[i].IsValid = true;
            }
            else if (table.HashEntries[i].SHA1String == "0000000000000000000000000000000000000000")
            {
                break;
            }
            else
            {
                table.HashEntries[i].IsValid = false;
                table.IsValid = false;
                Container.Resigned = false;
                Container.IsHeaderHashed = false;
            }
        }
    }

    public void ValidateMasterTable()
    {
        int firstOffset = Container.HashingBlock == 603979777 ? 40960 : 45056;

        Container.IO.Reader.BaseStream.Position = firstOffset;
        byte[] hash = StfsSigning.CalculateHash(
            Container.IO.Reader.ReadBytes(4096, EndianType.LittleEndian),
            Xbox360.HashAlgorithm.SHA1);
        string hashStr = StfsSigning.BytesToHexString(hash);

        if (SubTableHashes![0].SHA1String == hashStr)
        {
            SubTableHashes[0].IsValid = true;
            SubTables[0].IsValid = true;
        }
        else
        {
            SubTableHashes[0].IsValid = false;
            SubTables[0].IsValid = false;
        }

        IsValid = true;
        for (int i = 1; i < SubTables.Count; i++)
        {
            SubTables[i].IsValid = true;
            Container.IO.Reader.BaseStream.Position = 49152 + i * 704512;
            byte[] h = StfsSigning.CalculateHash(
                Container.IO.Reader.ReadBytes(4096, EndianType.LittleEndian),
                Xbox360.HashAlgorithm.SHA1);
            string hs = StfsSigning.BytesToHexString(h);

            if (hs == SubTableHashes[i].SHA1String)
            {
                SubTableHashes[i].IsValid = true;
            }
            else
            {
                SubTableHashes[i].IsValid = false;
                IsValid = false;
                SubTables[i].IsValid = false;
                Container.Rehashed = false;
            }
        }
    }

    public void FixEntry(HashEntry entry)
    {
        if (!entry.IsValid)
        {
            Container.IO.Reader.BaseStream.Position = entry.Validates;
            byte[] source = Container.IO.Reader.ReadBytes(4096, EndianType.LittleEndian);
            byte[] hash = StfsSigning.CalculateHash(source, Xbox360.HashAlgorithm.SHA1);
            Container.IO.Writer.BaseStream.Position = entry.Offset;
            Container.IO.Writer.Write(hash);
        }
    }

    public void ReHash()
    {
        foreach (var subTable in SubTables)
        {
            for (int i = 0; i < subTable.HashEntries.Count; i++)
            {
                if (subTable.HashEntries[i].SHA1String != "0000000000000000000000000000000000000000"
                    && !subTable.HashEntries[i].IsValid)
                {
                    FixEntry(subTable.HashEntries[i]);
                    subTable.HashEntries[i].IsValid = true;
                }
            }
            subTable.IsValid = true;
        }

        if (Container.HashCount < 170)
        {
            int hashOffset = Container.HashingBlock switch
            {
                603979777 => 40960,
                603980289 => 45056,
                _ => 40960
            };

            Container.IO.Reader.BaseStream.Position = hashOffset;
            byte[] source = Container.IO.Reader.ReadBytes(4096, EndianType.LittleEndian);
            byte[] hash = StfsSigning.CalculateHash(source, Xbox360.HashAlgorithm.SHA1);
            string hashStr = StfsSigning.BytesToHexString(hash);

            if (hashStr != Container.HashTableSHA1)
            {
                Container.IO.Writer.BaseStream.Position = 897;
                Container.IO.Writer.Write(hash);
            }
            return;
        }

        int masterOffset = Container.HashingBlock == 603979777 ? 745472 : 749568;
        int firstSubOffset = Container.HashingBlock == 603979777 ? 40960 : 45056;

        Container.IO.Reader.BaseStream.Position = firstSubOffset;
        byte[] firstHash = StfsSigning.CalculateHash(
            Container.IO.Reader.ReadBytes(4096, EndianType.LittleEndian),
            Xbox360.HashAlgorithm.SHA1);

        if (firstHash != SubTables[0].SHA1)
            SubTables[0].SHA1 = firstHash;

        Container.IO.Writer.BaseStream.Position = masterOffset;
        for (int i = 0; i < SubTables.Count; i++)
        {
            Container.IO.Writer.Write(SubTables[i].SHA1);
            Container.IO.Writer.BaseStream.Position += 4;
        }

        Container.IO.Reader.BaseStream.Position = masterOffset;
        byte[] masterHash = StfsSigning.CalculateHash(
            Container.IO.Reader.ReadBytes(4096, EndianType.LittleEndian),
            Xbox360.HashAlgorithm.SHA1);
        Container.IO.Writer.BaseStream.Position = 897;
        Container.IO.Writer.Write(masterHash);
    }
}
