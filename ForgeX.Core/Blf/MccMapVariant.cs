using ForgeX.Core.IO;
using ForgeX.Core.Halo3;

namespace ForgeX.Core.Blf;

/// <summary>
/// Reads and writes Halo 3 MCC .mvar files (BLF format).
/// Supports both unpacked mapv (byte-aligned) and packed mvar (bitstream) formats.
/// </summary>
public class MccMapVariant : IMapVariantData
{
    private BlfFile _blfFile;
    private byte[] _payload = Array.Empty<byte>();

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

    /// <summary>
    /// If the file was compressed, this is the path to the decompressed copy that was created.
    /// </summary>
    public string? DecompressedPath { get; private set; }

    // Packed header metadata (stored during LoadPacked for round-trip writing)
    private ulong _packedUniqueId;
    private int _packedFileType;
    private bool _packedAuthorIsXuidOnline;
    private ulong _packedAuthorId;
    private ulong _packedSizeInBytes;
    private ulong _packedDate;
    private uint _packedLengthSeconds;
    private int _packedCampaignId;
    private uint _packedGameEngineType;
    private int _packedCampaignDifficulty;
    private int _packedHopperId;
    private ulong _packedGameId;
    private int _packedVariantVersion;
    private uint _packedMapRsaHash;
    private int _packedNumberOfScenarioObjects;
    private int _packedNumberOfVariantObjects;
    private int _packedNumberOfQuotas;
    private bool _packedBuiltIn;
    private uint _packedGameEngineSubtype;
    private int[] _packedObjectTypeStartIndex = new int[14];

    // Unpacked mapv byte-aligned offsets (within chunk payload)
    // Payload starts with 12 bytes: unique_id(8) + unknown(4), then name
    private const int OffsetName = 12;          // UTF-16BE, 16 chars (32 bytes)
    private const int OffsetDesc = 44;          // ASCII, 128 bytes
    private const int OffsetAuthor = 172;       // ASCII, 16 bytes
    private const int OffsetMapIdMeta = 228;    // int32 BE (in content_item_metadata)
    private const int OffsetMapIdVariant = 260; // int32 BE (in map_variant_header)
    private const int OffsetWorldBounds = 264;  // 6 × float32 BE (24 bytes)
    private const int OffsetMaxBudget = 292;    // float32 BE
    private const int OffsetCurrentBudget = 296;// float32 BE
    private const int OffsetPlacements = 308;   // 640 × 84 bytes
    private const int PlacementSize = 84;
    private const int PlacementCount = 640;
    private const int OffsetTagIndex = 54420;   // 256 × 12 bytes
    private const int TagIndexEntrySize = 12;
    private const int TagIndexCount = 256;

    public MccMapVariant(string filePath)
    {
        _blfFile = new BlfFile(filePath);

        switch (_blfFile.VariantFormat)
        {
            case BlfVariantFormat.UnpackedMapv:
                LoadUnpacked();
                break;
            case BlfVariantFormat.PackedMvar:
                LoadPacked();
                break;
            case BlfVariantFormat.Compressed:
                DecompressedPath = BlfFile.Decompress(filePath);
                _blfFile = new BlfFile(DecompressedPath);
                if (_blfFile.VariantFormat == BlfVariantFormat.UnpackedMapv)
                    LoadUnpacked();
                else if (_blfFile.VariantFormat == BlfVariantFormat.PackedMvar)
                    LoadPacked();
                else
                    throw new InvalidDataException("Decompressed .mvar has unexpected format.");
                break;
            default:
                throw new InvalidDataException("Unrecognized .mvar format.");
        }
    }

    private void LoadUnpacked()
    {
        var chunk = _blfFile.GetChunk("mapv")
            ?? throw new InvalidDataException("Missing mapv chunk.");
        _payload = chunk.Data;

        using var ms = new MemoryStream(_payload);
        var reader = new EndianReader(ms, EndianType.BigEndian);

        // Read name (UTF-16BE, 16 chars at offset 8)
        reader.BaseStream.Position = OffsetName;
        VariantName = reader.XReadUnicodeString(16);

        // Read description (ASCII, 128 bytes at offset 44)
        reader.BaseStream.Position = OffsetDesc;
        VariantDescription = new string(reader.ReadChars(128, EndianType.LittleEndian)).Replace("\0", "");

        // Read author (ASCII, 16 bytes at offset 172)
        reader.BaseStream.Position = OffsetAuthor;
        MapAuthor = new string(reader.ReadChars(16, EndianType.LittleEndian)).Replace("\0", "");

        // Read MapId from content_item_metadata
        reader.BaseStream.Position = OffsetMapIdMeta;
        MapId = reader.ReadInt32();

        // Read SpawnedObjectCount
        reader.BaseStream.Position = 255;
        SpawnedObjectCount = reader.ReadByte();

        // Read world bounds (6 floats at offset 264)
        reader.BaseStream.Position = OffsetWorldBounds;
        WorldBoundsXMin = reader.ReadSingle();
        WorldBoundsXMax = reader.ReadSingle();
        WorldBoundsYMin = reader.ReadSingle();
        WorldBoundsYMax = reader.ReadSingle();
        WorldBoundsZMin = reader.ReadSingle();
        WorldBoundsZMax = reader.ReadSingle();

        // Read budgets
        reader.BaseStream.Position = OffsetMaxBudget;
        MaximumBudget = reader.ReadSingle();
        CurrentBudget = reader.ReadSingle();

        // Load tag database for this map
        Tags = new TagDatabase(MapId);

        // Read 640 placement chunks (same layout as Xbox 360 but with forward/up vectors)
        PlacementChunks = new List<PlacementChunk>(PlacementCount);
        for (int i = 0; i < PlacementCount; i++)
        {
            int off = OffsetPlacements + i * PlacementSize;
            reader.BaseStream.Position = off;
            var chunk2 = ReadPlacementUnpacked(reader, off);
            PlacementChunks.Add(chunk2);
        }

        // Read 256 tag index entries at offset 54420
        reader.BaseStream.Position = OffsetTagIndex;
        TagIndex = new List<TagIndexEntry>(TagIndexCount);
        for (int i = 0; i < TagIndexCount; i++)
        {
            var entry = new TagIndexEntry();
            entry.Read(reader, Tags);
            if (entry.Tag != null)
                entry.Tag.TagsIndex = i;
            else if (entry.Ident != 0 && entry.Ident != -1)
            {
                // Try palette lookup (MCC v13 files use palette indices instead of tag idents)
                var paletteTag = Tags?.FindTagByPaletteIndex(entry.Ident);
                if (paletteTag != null)
                {
                    entry.Tag = paletteTag;
                    entry.Tag.TagsIndex = i;
                }
            }
            TagIndex.Add(entry);
        }

        // Link placements to tag index entries
        LinkPlacements();
    }

    private PlacementChunk ReadPlacementUnpacked(EndianReader reader, int baseOffset)
    {
        var chunk = new PlacementChunk();
        chunk.Offset = baseOffset;

        chunk.ChunkType = (ChunkType)reader.ReadInt16();
        reader.BaseStream.Position += 10;
        chunk.TagsIndex = reader.ReadInt32();

        // Position
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        float z = reader.ReadSingle();

        // Forward vector (12 bytes)
        float fi = reader.ReadSingle();
        float fj = reader.ReadSingle();
        float fk = reader.ReadSingle();

        // Up vector (12 bytes)
        float ui = reader.ReadSingle();
        float uj = reader.ReadSingle();
        float uk = reader.ReadSingle();

        // Convert forward/up to yaw/pitch/roll for the UI
        var (yaw, pitch, roll) = OrientationConverter.ToYawPitchRoll(fi, fj, fk, ui, uj, uk);

        chunk.SpawnCoords = new SpawnCoords
        {
            X = x,
            Y = y,
            Z = z,
            Yaw = yaw,
            Pitch = pitch,
            Roll = roll
        };

        // Skip 10 bytes (offset 52-61 within placement)
        reader.BaseStream.Position += 10;

        // Flags, Team, SpareClips, RespawnTime (same positions as Xbox 360)
        chunk.Flags = reader.ReadByte();
        chunk.Team = reader.ReadByte();
        chunk.SpareClips = reader.ReadByte();
        chunk.RespawnTime = reader.ReadByte();

        // Skip remaining 18 bytes
        reader.BaseStream.Position += 18;

        return chunk;
    }

    private void LoadPacked()
    {
        var chunk = _blfFile.GetChunk("mvar")
            ?? throw new InvalidDataException("Missing mvar chunk.");
        _payload = chunk.Data;

        var bits = new BitReader(_payload);

        // === s_content_item_metadata ===
        // Field order and widths from Blam-Network/blf reference implementation
        _packedUniqueId = bits.ReadInteger64(64);        // unique_id
        VariantName = bits.ReadStringWchar(32);          // variable-length, up to 32 wchars
        VariantDescription = bits.ReadStringUtf8(128);   // variable-length, up to 128 bytes
        MapAuthor = bits.ReadStringUtf8(16);             // variable-length, up to 16 bytes
        _packedFileType = bits.ReadSignedInteger(5);     // file_type (signed 5 bits, stored as value+1)
        _packedAuthorIsXuidOnline = bits.ReadBool();     // author_is_xuid_online
        _packedAuthorId = bits.ReadInteger64(64);        // author_id
        _packedSizeInBytes = bits.ReadInteger64(64);     // size_in_bytes
        _packedDate = bits.ReadInteger64(64);            // date
        _packedLengthSeconds = bits.ReadInteger(32);     // length_seconds
        _packedCampaignId = bits.ReadSignedInteger(32);  // campaign_id
        MapId = bits.ReadSignedInteger(32);              // map_id
        _packedGameEngineType = bits.ReadInteger(4);     // game_engine_type (4 bits, not 32!)
        _packedCampaignDifficulty = bits.ReadSignedInteger(3); // campaign_difficulty (signed 3 bits, stored as value+1)
        _packedHopperId = bits.ReadSignedInteger(16);    // hopper_id
        _packedGameId = bits.ReadInteger64(64);          // game_id

        // === c_map_variant header ===
        _packedVariantVersion = (int)bits.ReadInteger(8);   // 8 bits (not 4!)
        _packedMapRsaHash = bits.ReadInteger(32);           // original_map_rsa_signature_hash
        _packedNumberOfScenarioObjects = (int)bits.ReadInteger(10); // scenario object count
        _packedNumberOfVariantObjects = (int)bits.ReadInteger(10);  // variant object count
        _packedNumberOfQuotas = (int)bits.ReadInteger(9);   // placeable quota count
        int headerMapId = (int)bits.ReadInteger(32);        // map_id (in variant header)
        _packedBuiltIn = bits.ReadBool();                   // built_in

        // World bounds: 6 × 32-bit raw floats (192 bits raw data)
        WorldBoundsXMin = bits.ReadRawFloat();
        WorldBoundsXMax = bits.ReadRawFloat();
        WorldBoundsYMin = bits.ReadRawFloat();
        WorldBoundsYMax = bits.ReadRawFloat();
        WorldBoundsZMin = bits.ReadRawFloat();
        WorldBoundsZMax = bits.ReadRawFloat();

        _packedGameEngineSubtype = bits.ReadInteger(4);    // game_engine_subtype (4 bits)
        MaximumBudget = bits.ReadRawFloat();             // 32-bit raw float
        CurrentBudget = bits.ReadRawFloat();             // 32-bit raw float

        // Load tag database
        Tags = new TagDatabase(MapId);

        // === variant_objects ===
        // The packed format iterates numberOfVariantObjects times (not always 640).
        // Each slot has a 1-bit exists flag, then conditional data.
        PlacementChunks = new List<PlacementChunk>(PlacementCount);
        for (int i = 0; i < _packedNumberOfVariantObjects; i++)
        {
            var placement = new PlacementChunk();
            placement.Offset = i;

            bool exists = bits.ReadBool();
            if (!exists)
            {
                placement.TagsIndex = -1;
                placement.ChunkType = ChunkType.Null;
                PlacementChunks.Add(placement);
                continue;
            }

            // Object exists: read flags and quota index
            placement.PackedFlags = (ushort)bits.ReadInteger(16);
            placement.Flags = (byte)(placement.PackedFlags & 0xFF);
            placement.TagsIndex = bits.ReadSignedInteger(32); // variant_quota_index

            // Parent object (conditional)
            placement.HasParentObject = bits.ReadBool();
            if (placement.HasParentObject)
            {
                placement.ParentObjectIdentifier = bits.ReadInteger64(64);
            }

            // Position data (conditional)
            placement.HasPosition = bits.ReadBool();
            if (!placement.HasPosition)
            {
                // Scenario object with unmodified position — no position/orientation data
                placement.ChunkType = ChunkType.Original;
                placement.SpawnCoords = new SpawnCoords();
                PlacementChunks.Add(placement);
                continue;
            }

            // Position: 3 × 16-bit quantized within world bounds
            float px = bits.ReadQuantizedReal(16, WorldBoundsXMin, WorldBoundsXMax, false);
            float py = bits.ReadQuantizedReal(16, WorldBoundsYMin, WorldBoundsYMax, false);
            float pz = bits.ReadQuantizedReal(16, WorldBoundsZMin, WorldBoundsZMax, false);

            // Axes: up vector (conditional global or 19-bit quantized) + 8-bit forward angle
            var (fi, fj, fk, ui, uj, uk) = OrientationConverter.ReadAxes(bits);

            // Convert to yaw/pitch/roll for the UI
            var (yaw, pitch, roll) = OrientationConverter.ToYawPitchRoll(fi, fj, fk, ui, uj, uk);

            placement.SpawnCoords = new SpawnCoords
            {
                X = px, Y = py, Z = pz,
                Yaw = yaw, Pitch = pitch, Roll = roll
            };

            // Multiplayer game object properties
            placement.ObjectType = bits.ReadSignedInteger(8);
            placement.SymmetryFlags = (byte)bits.ReadInteger(8);
            placement.GameEngineFlags = (ushort)bits.ReadInteger(16);
            placement.SpareClips = (byte)bits.ReadInteger(8);
            placement.RespawnTime = (byte)bits.ReadInteger(8);
            placement.Team = (byte)bits.ReadInteger(8);
            placement.BoundaryShape = (byte)bits.ReadInteger(8);

            // Boundary data (conditional based on shape)
            switch (placement.BoundaryShape)
            {
                case 1: // sphere
                    placement.BoundarySize = bits.ReadQuantizedReal(16, 0f, 60f, false);
                    placement.BoundaryNegativeHeight = bits.ReadQuantizedReal(16, 0f, 60f, false);
                    break;
                case 2: // cylinder
                    placement.BoundarySize = bits.ReadQuantizedReal(16, 0f, 60f, false);
                    placement.BoundaryBoxLength = bits.ReadQuantizedReal(16, 0f, 60f, false);
                    placement.BoundaryPositiveHeight = bits.ReadQuantizedReal(16, 0f, 60f, false);
                    break;
                case 3: // box
                    placement.BoundarySize = bits.ReadQuantizedReal(16, 0f, 60f, false);
                    placement.BoundaryBoxLength = bits.ReadQuantizedReal(16, 0f, 60f, false);
                    placement.BoundaryPositiveHeight = bits.ReadQuantizedReal(16, 0f, 60f, false);
                    placement.BoundaryNegativeHeight = bits.ReadQuantizedReal(16, 0f, 60f, false);
                    break;
            }

            placement.ChunkType = i < _packedNumberOfScenarioObjects ? ChunkType.Edited : ChunkType.Added;
            PlacementChunks.Add(placement);
        }

        // Fill remaining slots up to 640 with empty entries
        for (int i = PlacementChunks.Count; i < PlacementCount; i++)
        {
            PlacementChunks.Add(new PlacementChunk
            {
                Offset = i,
                TagsIndex = -1,
                ChunkType = ChunkType.Null
            });
        }

        SpawnedObjectCount = (byte)PlacementChunks.Count(p => p.TagsIndex >= 0);

        // === object_type_start_index[14] ===
        // Each is 9 bits, stored as value+1
        for (int i = 0; i < 14; i++)
            _packedObjectTypeStartIndex[i] = (int)bits.ReadInteger(9);

        // === quotas (tag index entries) ===
        TagIndex = new List<TagIndexEntry>(TagIndexCount);
        for (int i = 0; i < _packedNumberOfQuotas && i < TagIndexCount; i++)
        {
            var entry = new TagIndexEntry();
            entry.Offset = i;
            entry.Ident = (int)bits.ReadInteger(32);     // object_definition_index (unsigned 32)
            entry.Tag = Tags?.FindTag(entry.Ident);
            if (entry.Tag != null)
                entry.Tag.TagsIndex = i;
            else if (entry.Ident != 0 && entry.Ident != -1)
            {
                // Try palette lookup (MCC v13 files use palette indices instead of tag idents)
                var paletteTag = Tags?.FindTagByPaletteIndex(entry.Ident);
                if (paletteTag != null)
                {
                    entry.Tag = paletteTag;
                    entry.Tag.TagsIndex = i;
                }
                // Entries with palette types beyond the 7 known categories (Vehicle-Spawner)
                // are engine-internal and have no palette data — leave Tag null so they
                // don't appear in the tag tree.
            }
            entry.RunTimeMinimum = (byte)bits.ReadInteger(8);    // minimum_count
            entry.RunTimeMaximum = (byte)bits.ReadInteger(8);    // maximum_count
            entry.CountOnMap = (byte)bits.ReadInteger(8);        // placed_on_map
            entry.DesignTimeMaximum = (byte)bits.ReadInteger(8); // maximum_allowed (signed 8 in Rust, byte here)
            entry.Cost = bits.ReadRawFloat();                    // price_per_item (32-bit raw float)
            TagIndex.Add(entry);
        }

        // Fill remaining quota slots with empty entries
        for (int i = TagIndex.Count; i < TagIndexCount; i++)
        {
            TagIndex.Add(new TagIndexEntry
            {
                Offset = i,
                Ident = -1
            });
        }

        // Link placements to tag index entries
        LinkPlacements();
    }

    private void LinkPlacements()
    {
        for (int i = 0; i < TagIndex.Count; i++)
        {
            for (int j = 0; j < PlacementChunks.Count; j++)
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
        if (_blfFile.VariantFormat == BlfVariantFormat.PackedMvar)
        {
            WritePacked();
            return;
        }

        using var ms = new MemoryStream(_payload);
        var writer = new EndianWriter(ms, EndianType.BigEndian);
        WriteHeaderToPayload(writer);
        FlushToFile();
    }

    public void WritePlacement(PlacementChunk chunk)
    {
        if (_blfFile.VariantFormat == BlfVariantFormat.PackedMvar)
        {
            WritePacked();
            return;
        }

        using var ms = new MemoryStream(_payload);
        var writer = new EndianWriter(ms, EndianType.BigEndian);
        WritePlacementToPayload(writer, chunk);
        FlushToFile();
    }

    public void WriteTagIndexEntry(TagIndexEntry entry)
    {
        if (_blfFile.VariantFormat == BlfVariantFormat.PackedMvar)
        {
            WritePacked();
            return;
        }

        using var ms = new MemoryStream(_payload);
        var writer = new EndianWriter(ms, EndianType.BigEndian);
        WriteTagEntryToPayload(writer, entry, TagIndex.IndexOf(entry));
        FlushToFile();
    }

    public void SaveAll()
    {
        if (_blfFile.VariantFormat == BlfVariantFormat.PackedMvar)
        {
            WritePacked();
            return;
        }

        using var ms = new MemoryStream(_payload);
        var writer = new EndianWriter(ms, EndianType.BigEndian);

        WriteHeaderToPayload(writer);

        for (int i = 0; i < PlacementChunks.Count && i < PlacementCount; i++)
            WritePlacementToPayload(writer, PlacementChunks[i]);

        for (int i = 0; i < TagIndex.Count && i < TagIndexCount; i++)
            WriteTagEntryToPayload(writer, TagIndex[i], i);

        FlushToFile();
    }

    private void WriteHeaderToPayload(EndianWriter writer)
    {
        writer.BaseStream.Position = OffsetName;
        WriteUnicodeBE(writer, VariantName, 16);

        writer.BaseStream.Position = OffsetDesc;
        WriteAsciiBE(writer, VariantDescription, 128);

        writer.BaseStream.Position = OffsetAuthor;
        WriteAsciiBE(writer, MapAuthor, 16);

        writer.BaseStream.Position = OffsetMaxBudget;
        writer.Write(BitConverter.GetBytes(MaximumBudget).Reverse().ToArray());
        writer.Write(BitConverter.GetBytes(CurrentBudget).Reverse().ToArray());
    }

    private static void WritePlacementToPayload(EndianWriter writer, PlacementChunk chunk)
    {
        writer.BaseStream.Position = chunk.Offset;

        // ChunkType
        byte[] ctBytes = BitConverter.GetBytes((short)chunk.ChunkType);
        if (BitConverter.IsLittleEndian) Array.Reverse(ctBytes);
        writer.Write(ctBytes);

        // Skip 10 bytes
        writer.BaseStream.Position += 10;

        // TagsIndex
        byte[] tiBytes = BitConverter.GetBytes(chunk.TagsIndex);
        if (BitConverter.IsLittleEndian) Array.Reverse(tiBytes);
        writer.Write(tiBytes);

        // Convert yaw/pitch/roll back to forward/up vectors
        var (fi, fj, fk, ui, uj, uk) = OrientationConverter.ToForwardUp(
            chunk.SpawnCoords.Yaw, chunk.SpawnCoords.Pitch, chunk.SpawnCoords.Roll);

        // Write position
        WriteBEFloat(writer, chunk.SpawnCoords.X);
        WriteBEFloat(writer, chunk.SpawnCoords.Y);
        WriteBEFloat(writer, chunk.SpawnCoords.Z);

        // Write forward vector
        WriteBEFloat(writer, fi);
        WriteBEFloat(writer, fj);
        WriteBEFloat(writer, fk);

        // Write up vector
        WriteBEFloat(writer, ui);
        WriteBEFloat(writer, uj);
        WriteBEFloat(writer, uk);

        // Skip 10 bytes
        writer.BaseStream.Position += 10;

        // Flags, Team, SpareClips, RespawnTime
        writer.Write(chunk.Flags);
        writer.Write(chunk.Team);
        writer.Write(chunk.SpareClips);
        writer.Write(chunk.RespawnTime);
    }

    private static void WriteTagEntryToPayload(EndianWriter writer, TagIndexEntry entry, int index)
    {
        int offset = OffsetTagIndex + index * TagIndexEntrySize;
        writer.BaseStream.Position = offset;

        // Ident (big-endian)
        byte[] identBytes = BitConverter.GetBytes(entry.Ident);
        if (BitConverter.IsLittleEndian) Array.Reverse(identBytes);
        writer.Write(identBytes);

        writer.Write(entry.RunTimeMinimum);
        writer.Write(entry.RunTimeMaximum);
        writer.Write(entry.CountOnMap);
        writer.Write(entry.DesignTimeMaximum);

        // Cost (big-endian float)
        WriteBEFloat(writer, entry.Cost);
    }

    private void WritePacked()
    {
        var bits = new BitWriter();

        // === s_content_item_metadata ===
        bits.WriteInteger64(_packedUniqueId, 64);
        bits.WriteStringWchar(VariantName, 32);
        bits.WriteStringUtf8(VariantDescription, 128);
        bits.WriteStringUtf8(MapAuthor, 16);
        bits.WriteSignedInteger(_packedFileType, 5);
        bits.WriteBool(_packedAuthorIsXuidOnline);
        bits.WriteInteger64(_packedAuthorId, 64);
        bits.WriteInteger64(_packedSizeInBytes, 64);
        bits.WriteInteger64(_packedDate, 64);
        bits.WriteInteger(_packedLengthSeconds, 32);
        bits.WriteSignedInteger(_packedCampaignId, 32);
        bits.WriteSignedInteger(MapId, 32);
        bits.WriteInteger(_packedGameEngineType, 4);
        bits.WriteSignedInteger(_packedCampaignDifficulty, 3);
        bits.WriteSignedInteger(_packedHopperId, 16);
        bits.WriteInteger64(_packedGameId, 64);

        // === c_map_variant header ===
        bits.WriteInteger((uint)_packedVariantVersion, 8);
        bits.WriteInteger(_packedMapRsaHash, 32);
        bits.WriteInteger((uint)_packedNumberOfScenarioObjects, 10);
        bits.WriteInteger((uint)_packedNumberOfVariantObjects, 10);

        // Recount quotas from current TagIndex (number of non-empty entries)
        int quotaCount = 0;
        for (int i = 0; i < TagIndex.Count; i++)
        {
            if (TagIndex[i].Ident != -1 && TagIndex[i].Ident != 0)
                quotaCount = i + 1;
        }
        if (quotaCount > _packedNumberOfQuotas)
            _packedNumberOfQuotas = quotaCount;
        bits.WriteInteger((uint)_packedNumberOfQuotas, 9);

        bits.WriteInteger((uint)MapId, 32);
        bits.WriteBool(_packedBuiltIn);

        // World bounds: 6 × 32-bit raw floats
        bits.WriteRawFloat(WorldBoundsXMin);
        bits.WriteRawFloat(WorldBoundsXMax);
        bits.WriteRawFloat(WorldBoundsYMin);
        bits.WriteRawFloat(WorldBoundsYMax);
        bits.WriteRawFloat(WorldBoundsZMin);
        bits.WriteRawFloat(WorldBoundsZMax);

        bits.WriteInteger(_packedGameEngineSubtype, 4);
        bits.WriteRawFloat(MaximumBudget);
        bits.WriteRawFloat(CurrentBudget);

        // === variant_objects ===
        for (int i = 0; i < _packedNumberOfVariantObjects; i++)
        {
            var placement = i < PlacementChunks.Count ? PlacementChunks[i] : null;
            bool exists = placement != null && placement.TagsIndex >= 0;

            bits.WriteBool(exists);
            if (!exists)
                continue;

            // Flags and quota index
            bits.WriteInteger(placement!.PackedFlags, 16);
            bits.WriteSignedInteger(placement.TagsIndex, 32);

            // Parent object (conditional)
            bits.WriteBool(placement.HasParentObject);
            if (placement.HasParentObject)
            {
                bits.WriteInteger64(placement.ParentObjectIdentifier, 64);
            }

            // Position data (conditional)
            bits.WriteBool(placement.HasPosition);
            if (!placement.HasPosition)
                continue;

            // Position: 3 × 16-bit quantized within world bounds
            bits.WriteQuantizedReal(placement.SpawnCoords.X, 16, WorldBoundsXMin, WorldBoundsXMax, false);
            bits.WriteQuantizedReal(placement.SpawnCoords.Y, 16, WorldBoundsYMin, WorldBoundsYMax, false);
            bits.WriteQuantizedReal(placement.SpawnCoords.Z, 16, WorldBoundsZMin, WorldBoundsZMax, false);

            // Axes: convert yaw/pitch/roll to forward/up, then encode
            var (fi, fj, fk, ui, uj, uk) = OrientationConverter.ToForwardUp(
                placement.SpawnCoords.Yaw, placement.SpawnCoords.Pitch, placement.SpawnCoords.Roll);
            OrientationConverter.WriteAxes(bits, fi, fj, fk, ui, uj, uk);

            // Multiplayer game object properties
            bits.WriteSignedInteger(placement.ObjectType, 8);
            bits.WriteInteger(placement.SymmetryFlags, 8);
            bits.WriteInteger(placement.GameEngineFlags, 16);
            bits.WriteInteger(placement.SpareClips, 8);
            bits.WriteInteger(placement.RespawnTime, 8);
            bits.WriteInteger(placement.Team, 8);
            bits.WriteInteger(placement.BoundaryShape, 8);

            // Boundary data (conditional based on shape)
            switch (placement.BoundaryShape)
            {
                case 1: // sphere
                    bits.WriteQuantizedReal(placement.BoundarySize, 16, 0f, 60f, false);
                    bits.WriteQuantizedReal(placement.BoundaryNegativeHeight, 16, 0f, 60f, false);
                    break;
                case 2: // cylinder
                    bits.WriteQuantizedReal(placement.BoundarySize, 16, 0f, 60f, false);
                    bits.WriteQuantizedReal(placement.BoundaryBoxLength, 16, 0f, 60f, false);
                    bits.WriteQuantizedReal(placement.BoundaryPositiveHeight, 16, 0f, 60f, false);
                    break;
                case 3: // box
                    bits.WriteQuantizedReal(placement.BoundarySize, 16, 0f, 60f, false);
                    bits.WriteQuantizedReal(placement.BoundaryBoxLength, 16, 0f, 60f, false);
                    bits.WriteQuantizedReal(placement.BoundaryPositiveHeight, 16, 0f, 60f, false);
                    bits.WriteQuantizedReal(placement.BoundaryNegativeHeight, 16, 0f, 60f, false);
                    break;
            }
        }

        // === object_type_start_index[14] ===
        for (int i = 0; i < 14; i++)
            bits.WriteInteger((uint)_packedObjectTypeStartIndex[i], 9);

        // === quotas (tag index entries) ===
        for (int i = 0; i < _packedNumberOfQuotas; i++)
        {
            var entry = i < TagIndex.Count ? TagIndex[i] : new TagIndexEntry { Ident = -1 };
            bits.WriteInteger((uint)entry.Ident, 32);
            bits.WriteInteger(entry.RunTimeMinimum, 8);
            bits.WriteInteger(entry.RunTimeMaximum, 8);
            bits.WriteInteger(entry.CountOnMap, 8);
            bits.WriteInteger(entry.DesignTimeMaximum, 8);
            bits.WriteRawFloat(entry.Cost);
        }

        _payload = bits.ToArray();
        FlushToFile();
    }

    private void FlushToFile()
    {
        string chunkTag = _blfFile.VariantFormat == BlfVariantFormat.UnpackedMapv ? "mapv" : "mvar";
        _blfFile.UpdateChunkData(chunkTag, _payload);
        _blfFile.Write();
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

    public void CloseIO()
    {
        // No persistent streams to close for MCC format
    }

    private static void WriteBEFloat(EndianWriter writer, float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        writer.Write(bytes);
    }

    private static void WriteUnicodeBE(EndianWriter writer, string text, int maxChars)
    {
        for (int i = 0; i < maxChars; i++)
        {
            ushort c = i < text.Length ? (ushort)text[i] : (ushort)0;
            byte[] bytes = BitConverter.GetBytes(c);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            writer.Write(bytes);
        }
    }

    private static void WriteAsciiBE(EndianWriter writer, string text, int maxBytes)
    {
        for (int i = 0; i < maxBytes; i++)
        {
            byte b = i < text.Length ? (byte)text[i] : (byte)0;
            writer.Write(b);
        }
    }
}
