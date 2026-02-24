using ForgeX.Core.IO;
using ForgeX.Core.Xbox360;

namespace ForgeX.Core.Halo3;

/// <summary>
/// Reads and writes Halo 3 sandbox.map (usermap/forge variant) data from an Xbox 360 STFS container.
/// All offsets match the original Forge tool for binary compatibility.
/// </summary>
public class MapVariant : IMapVariantData
{
    public EndianReader Reader { get; set; } = null!;
    public EndianWriter Writer { get; set; } = null!;
    public StfsContainer Container { get; set; } = null!;

    public string VariantName { get; set; } = string.Empty;
    public string VariantDescription { get; set; } = string.Empty;
    public string MapAuthor { get; set; } = string.Empty;
    public int MapId { get; set; }
    public byte SpawnedObjectCount { get; set; }

    public float WorldBoundsXMin { get; set; }
    public float WorldBoundsXMax { get; set; }
    public float WorldBoundsYMin { get; set; }
    public float WorldBoundsYMax { get; set; }
    public float WorldBoundsZMin { get; set; }
    public float WorldBoundsZMax { get; set; }

    public float MaximumBudget { get; set; }
    public float CurrentBudget { get; set; }

    public List<TagIndexEntry> TagIndex { get; set; } = new();
    public List<PlacementChunk> PlacementChunks { get; set; } = new();
    public TagDatabase? Tags { get; set; }
    public bool CanWrite => true;

    private MemoryStream? _sandboxStream;

    public MapVariant(StfsContainer container)
    {
        Container = container;
        var entry = container.GetEntryByFileName("sandbox.map")
            ?? throw new InvalidDataException("Container does not contain a sandbox.map file.");

        var data = entry.GetData();
        _sandboxStream = new MemoryStream(data);
        Reader = new EndianReader(_sandboxStream, EndianType.BigEndian);
        Writer = new EndianWriter(_sandboxStream, EndianType.LittleEndian);
        LoadVariant();
    }

    public void CloseIO()
    {
        Container.Close();
    }

    public void LoadVariant()
    {
        // Read variant metadata
        Reader.BaseStream.Position = 336;
        VariantName = Reader.XReadUnicodeString(16);

        Reader.BaseStream.Position = 368;
        VariantDescription = new string(Reader.ReadChars(128, EndianType.LittleEndian)).Replace("\0", "");

        Reader.BaseStream.Position = 496;
        MapAuthor = new string(Reader.ReadChars(16, EndianType.LittleEndian)).Replace("\0", "");

        Reader.BaseStream.Position = 552;
        MapId = Reader.ReadInt32();

        Reader.BaseStream.Position = 583;
        SpawnedObjectCount = Reader.ReadByte();

        // Skip 4 bytes
        Reader.BaseStream.Position += 4;
        WorldBoundsXMin = Reader.ReadSingle();
        WorldBoundsXMax = Reader.ReadSingle();
        WorldBoundsYMin = Reader.ReadSingle();
        WorldBoundsYMax = Reader.ReadSingle();
        WorldBoundsZMin = Reader.ReadSingle();
        WorldBoundsZMax = Reader.ReadSingle();

        // Skip 4 bytes
        Reader.BaseStream.Position += 4;
        MaximumBudget = Reader.ReadSingle();
        CurrentBudget = Reader.ReadSingle();

        // Skip 8 bytes
        Reader.BaseStream.Position += 8;

        // Load tag definitions for this map
        Tags = new TagDatabase(MapId);

        // Read 640 placement chunks
        PlacementChunks = new List<PlacementChunk>(640);
        for (int i = 0; i < 640; i++)
        {
            var chunk = new PlacementChunk();
            chunk.Read(Reader);
            PlacementChunks.Add(chunk);
        }

        // Read 256 tag index entries at offset 54420
        Reader.BaseStream.Position = 54420;
        TagIndex = new List<TagIndexEntry>(256);
        for (int i = 0; i < 256; i++)
        {
            var entry = new TagIndexEntry();
            entry.Read(Reader, Tags);
            if (entry.Tag != null)
                entry.Tag.TagsIndex = i;
            TagIndex.Add(entry);
        }

        // Link placements to their tag index entries
        for (int i = 0; i < 256; i++)
        {
            for (int j = 0; j < 640; j++)
            {
                if (PlacementChunks[j].TagsIndex == i)
                {
                    TagIndex[i].PlacedItems.Add(PlacementChunks[j]);
                    PlacementChunks[j].Entry = TagIndex[i];
                }
            }
        }
    }

    public void WriteHeader()
    {
        WriteHeaderToStream();
        FlushToContainer();
    }

    public void WritePlacement(PlacementChunk chunk)
    {
        chunk.Write(Writer);
        FlushToContainer();
    }

    public void WriteTagIndexEntry(TagIndexEntry entry)
    {
        entry.Write(Writer);
        FlushToContainer();
    }

    public void SaveAll()
    {
        WriteHeaderToStream();

        foreach (var chunk in PlacementChunks)
            chunk.Write(Writer);

        foreach (var entry in TagIndex)
            entry.Write(Writer);

        FlushToContainer();
    }

    private void WriteHeaderToStream()
    {
        // Write at both header locations (offsets 72 and 336)
        Writer.BaseStream.Position = 72;
        Writer.WriteUnicode(VariantName, 16);
        Writer.BaseStream.Position = 104;
        Writer.Write(VariantDescription, 128);
        Writer.BaseStream.Position = 232;
        Writer.Write(MapAuthor, 16);

        Writer.BaseStream.Position = 336;
        Writer.WriteUnicode(VariantName, 16);
        Writer.BaseStream.Position = 368;
        Writer.Write(VariantDescription, 128);
        Writer.BaseStream.Position = 496;
        Writer.Write(MapAuthor, 16);

        Writer.BaseStream.Position = 616;
        Writer.WriteFloat(MaximumBudget);
        Writer.WriteFloat(CurrentBudget);
    }

    private void FlushToContainer()
    {
        var entry = Container.GetEntryByFileName("sandbox.map");
        if (entry == null) return;

        Reader.BaseStream.Position = 0;
        var data = new byte[Reader.BaseStream.Length];
        for (int i = 0; i < data.Length; i++)
            data[i] = Reader.ReadByte();
        entry.WriteData(data);
    }

    public TagIndexEntry? FindTagIndexEntry(string tagClass, string tagPath, int tagsIndex)
    {
        for (int i = 0; i < TagIndex.Count; i++)
        {
            if (TagIndex[i].Tag != null &&
                TagIndex[i].Tag!.Path == tagPath &&
                TagIndex[i].Tag!.Class == tagClass &&
                TagIndex[i].Tag!.TagsIndex == tagsIndex)
            {
                return TagIndex[i];
            }
        }
        return null;
    }
}
