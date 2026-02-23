using ForgeX.Core.IO;

namespace ForgeX.Core.Xbox360;

public class ContainerFileEntry
{
    public bool IsValid => Flags != 0;
    public uint TotalBlocks { get; set; }
    public uint TotalAllocatedBlocks { get; set; }
    public uint Cluster { get; set; }
    public int Created { get; set; }
    public int LastModified { get; set; }
    public string FileName { get; set; } = string.Empty;
    public byte Flags { get; set; }
    public StfsContainer Parent { get; set; } = null!;
    public ushort DirectoryIndex { get; set; }
    public int Size { get; set; }
    private List<DataContainer>? _dataBlocks;

    public ContainerFileEntry(StfsContainer container)
    {
        Parent = container;
    }

    public void Read()
    {
        FileName = Parent.IO.Reader.ReadString(40);
        Flags = Parent.IO.Reader.ReadByte();
        TotalAllocatedBlocks = (uint)Parent.IO.Reader.ReadInt24(EndianType.LittleEndian);
        TotalBlocks = (uint)Parent.IO.Reader.ReadInt24(EndianType.LittleEndian);
        Cluster = (uint)Parent.IO.Reader.ReadInt24(EndianType.LittleEndian);
        DirectoryIndex = Parent.IO.Reader.ReadUInt16();
        Size = Parent.IO.Reader.ReadInt32();
        Created = Parent.IO.Reader.ReadInt32();
        LastModified = Parent.IO.Reader.ReadInt32();
    }

    public byte[] GetData()
    {
        if (Parent.HashCount <= 170)
        {
            Parent.IO.Reader.BaseStream.Position = 49152 + Cluster * 4096;
            return Parent.IO.Reader.XReadBytes(Size);
        }

        var result = new List<byte>();
        _dataBlocks = new List<DataContainer>();
        long fullBlocks = Size >> 12;
        long remainder = Size - (fullBlocks << 12);

        for (long c = Cluster; c < Cluster + fullBlocks; c++)
        {
            long offset = GetOffsetLong(c);
            Parent.IO.Reader.BaseStream.Seek(offset, SeekOrigin.Begin);
            byte[] block = Parent.IO.Reader.ReadBytes(4096, EndianType.LittleEndian);
            _dataBlocks.Add(new DataContainer { Data = block, Offset = offset });
            result.AddRange(block);
        }

        Parent.IO.Reader.BaseStream.Position = GetOffsetLong(Cluster + fullBlocks);
        long pos = Parent.IO.Reader.BaseStream.Position;
        byte[] lastBlock = Parent.IO.Reader.ReadBytes((int)remainder, EndianType.LittleEndian);
        _dataBlocks.Add(new DataContainer { Data = lastBlock, Offset = pos });
        result.AddRange(lastBlock);

        return result.ToArray();
    }

    public void WriteData(byte[] data)
    {
        if (Parent.HashCount <= 170)
        {
            Parent.IO.Writer.BaseStream.Position = GetOffset();
            Parent.IO.Writer.Write(data);
            return;
        }

        if (_dataBlocks == null)
        {
            GetData(); // Initialize data blocks
        }

        // Split data into 4096-byte blocks
        for (int i = 0; i < data.Length / 4096; i++)
        {
            _dataBlocks![i].Data = new byte[4096];
            Array.Copy(data, i * 4096, _dataBlocks[i].Data, 0, 4096);
        }

        if (data.Length % 4096 > 0)
        {
            int lastIdx = _dataBlocks!.Count - 1;
            _dataBlocks[lastIdx].Data = new byte[data.Length % 4096];
            Array.Copy(data, lastIdx * 4096, _dataBlocks[lastIdx].Data, 0, data.Length % 4096);
        }

        // Write all blocks
        for (int i = 0; i < _dataBlocks!.Count; i++)
        {
            Parent.IO.Writer.BaseStream.Position = _dataBlocks[i].Offset;
            Parent.IO.Writer.Write(_dataBlocks[i].Data);
        }
    }

    public int GetOffset() => (int)GetOffsetLong(Cluster);

    private long GetOffsetLong(long cluster)
    {
        long offset = 49152 + cluster * 4096;
        long l1 = cluster / 170;
        long l2 = l1 / 170;

        if (l1 > 0)
            offset += (l1 + 1) * 8192;
        if (l2 > 0)
            offset += (l2 + 1) * 8192;

        return offset;
    }
}
