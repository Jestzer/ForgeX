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

    // Packed mvar additional fields (not used by Xbox 360 or unpacked mapv paths)
    public int VariantIndex { get; set; } = -1;
    public ushort PackedFlags { get; set; }
    public bool HasParentObject { get; set; }
    public ulong ParentObjectIdentifier { get; set; }
    public bool HasPosition { get; set; } = true;
    public int ObjectType { get; set; }
    public byte SymmetryFlags { get; set; }
    public ushort GameEngineFlags { get; set; }
    public byte BoundaryShape { get; set; }
    public float BoundarySize { get; set; }
    public float BoundaryBoxLength { get; set; }
    public float BoundaryPositiveHeight { get; set; }
    public float BoundaryNegativeHeight { get; set; }

    // Reach-specific fields (round-trip storage for write support)
    public int SpawnRelativeTo { get; set; } = -1;
    public byte SpawnSequence { get; set; }
    public int LabelIndex { get; set; } = -1;
    public int PrimaryColorIndex { get; set; } = -1;
    public byte TeleporterChannel { get; set; }
    public byte TeleporterPassability { get; set; }
    public int LocationNameIndex { get; set; } = -1;
    public uint ReachTeamRaw { get; set; } // Raw 4-bit team value for Reach/H4 round-trip

    // Raw packed encoding for Reach/H4 lossless round-trip
    public bool HasRawPackedData { get; set; }         // True when raw encoded values are available
    public uint RawPositionX { get; set; }             // Raw quantized X position
    public uint RawPositionY { get; set; }             // Raw quantized Y position
    public uint RawPositionZ { get; set; }             // Raw quantized Z position
    public bool OrientationAxisIsDefault { get; set; } // 1-bit: axis is (0,0,1)
    public uint OrientationAxisRaw { get; set; }       // 20-bit encoded axis (if not default)
    public uint OrientationAngleRaw { get; set; }      // 14-bit encoded angle

    // Halo 4-specific fields (round-trip storage)
    public byte H4ScaleRaw { get; set; }                // 6-bit raw scale value
    public bool H4IsLocked { get; set; }                // 1-bit locked state
    public ushort H4Unk10 { get; set; }                 // 10-bit unknown field
    public int LabelIndex2 { get; set; } = -1;          // H4 second label slot
    public int LabelIndex3 { get; set; } = -1;          // H4 third label slot
    public int LabelIndex4 { get; set; } = -1;          // H4 fourth label slot
    public byte[]? H4TypeConditionalData { get; set; }  // Raw type-conditional bits for round-trip

    // Flag bit accessors — definitions from Mjolnir ForgeLib/ForgeObject.cs
    public bool HideAtStart
    {
        get => (Flags & 0x02) != 0;
        set => Flags = (byte)(value ? Flags | 0x02 : Flags & ~0x02);
    }

    public bool Symmetric
    {
        get => (Flags & 0x04) != 0;
        set => Flags = (byte)(value ? Flags | 0x04 : Flags & ~0x04);
    }

    public bool Asymmetric
    {
        get => (Flags & 0x08) != 0;
        set => Flags = (byte)(value ? Flags | 0x08 : Flags & ~0x08);
    }

    public bool GameSpecific
    {
        get => (Flags & 0x20) != 0;
        set => Flags = (byte)(value ? Flags | 0x20 : Flags & ~0x20);
    }

    /// <summary>
    /// Physics mode from bits 7-6 (0xC0 mask): 0=Normal, 1=Fixed, 3=Phased.
    /// </summary>
    public byte PhysicsMode
    {
        get => (byte)((Flags >> 6) & 0x03);
        set => Flags = (byte)((Flags & ~0xC0) | ((value & 0x03) << 6));
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
