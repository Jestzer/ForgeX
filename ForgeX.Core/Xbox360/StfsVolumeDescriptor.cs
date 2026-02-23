using ForgeX.Core.IO;

namespace ForgeX.Core.Xbox360;

public class StfsVolumeDescriptor
{
    public byte Reversed { get; set; }
    public byte BlockSeparation { get; set; }
    public int DirectoryRelocator { get; set; }
    public uint TotalAlloc { get; set; }

    public StfsVolumeDescriptor(byte[] data)
    {
        using var ms = new MemoryStream(data);
        var reader = new EndianReader(ms, EndianType.BigEndian);

        byte _ = reader.ReadByte(); // first byte unused
        Reversed = reader.ReadByte();
        BlockSeparation = reader.ReadByte();
        short __ = reader.ReadInt16(); // unused short
        DirectoryRelocator = reader.ReadInt24(EndianType.LittleEndian);
        byte[] ___ = reader.ReadBytes(20, EndianType.LittleEndian); // hash
        TotalAlloc = reader.ReadUInt32();
        uint ____ = reader.ReadUInt32(); // unused
    }
}
