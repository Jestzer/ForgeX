using ForgeX.Core.IO;

namespace ForgeX.Core.Halo3;

public class PlacementChunk
{
    public int Offset { get; set; }
    public ChunkType ChunkType { get; set; }
    public int TagsIndex { get; set; }
    public SpawnCoords SpawnCoords { get; set; } = new();
    public byte Flags { get; set; }
    public byte Team { get; set; }
    public byte SpareClips { get; set; }
    public byte RespawnTime { get; set; }
    public TagIndexEntry? Entry { get; set; }

    // Flag bit accessors (bits 1, 2, 3 from the original UI)
    public bool Flag1
    {
        get => (Flags & 0x02) != 0;
        set => Flags = (byte)(value ? Flags | 0x02 : Flags & ~0x02);
    }

    public bool Flag2
    {
        get => (Flags & 0x04) != 0;
        set => Flags = (byte)(value ? Flags | 0x04 : Flags & ~0x04);
    }

    public bool Flag3
    {
        get => (Flags & 0x08) != 0;
        set => Flags = (byte)(value ? Flags | 0x08 : Flags & ~0x08);
    }

    public void Read(EndianReader reader)
    {
        Offset = (int)reader.BaseStream.Position;
        ChunkType = (ChunkType)reader.ReadInt16();
        reader.BaseStream.Position += 10;
        TagsIndex = reader.ReadInt32();

        SpawnCoords = new SpawnCoords
        {
            X = reader.ReadSingle(),
            Y = reader.ReadSingle(),
            Z = reader.ReadSingle(),
            Yaw = reader.ReadSingle(),
            Pitch = reader.ReadSingle(),
            Roll = reader.ReadSingle()
        };

        reader.BaseStream.Position += 22;
        Flags = reader.ReadByte();
        Team = reader.ReadByte();
        SpareClips = reader.ReadByte();
        RespawnTime = reader.ReadByte();
        reader.BaseStream.Position += 18;
    }

    public void Write(EndianWriter writer)
    {
        writer.BaseStream.Position = Offset;
        writer.Write((short)ChunkType);
        writer.BaseStream.Position += 10;
        writer.WriteIdent(TagsIndex);
        writer.WriteFloat(SpawnCoords.X);
        writer.WriteFloat(SpawnCoords.Y);
        writer.WriteFloat(SpawnCoords.Z);
        writer.WriteFloat(SpawnCoords.Yaw);
        writer.WriteFloat(SpawnCoords.Pitch);
        writer.WriteFloat(SpawnCoords.Roll);
        writer.BaseStream.Position += 22;
        writer.Write(Flags);
        writer.Write(Team);
        writer.Write(SpareClips);
        writer.Write(RespawnTime);
        writer.BaseStream.Position += 18;
    }
}
