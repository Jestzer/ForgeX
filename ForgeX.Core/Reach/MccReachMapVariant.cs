using ForgeX.Core.IO;
using ForgeX.Core.Halo3;
using ForgeX.Core.Blf;

namespace ForgeX.Core.Reach;

/// <summary>
/// Reads and writes Halo: Reach MCC .mvar files (BLF packed mvar, version 31).
/// Reach uses fundamentally different bitstream encoding from Halo 3:
/// adaptive per-axis position encoding, 20-bit axis-angle orientation,
/// 651 object slots, integer budgets, and simpler quotas (no ident/cost).
/// </summary>
public class MccReachMapVariant : IMapVariantData
{
    private const int VariantObjectCount = 651;
    private const int MaxQuotas = 256;

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
    public TagDatabase? Tags => null;
    public bool CanWrite => true;
    public ReachPaletteDatabase? Palette { get; private set; }

    private int _numberOfQuotas;

    // BLF container reference for writing back
    private BlfFile _blfFile;
    private byte[] _prefix = new byte[24]; // SHA-1 hash (20 bytes) + packed length (4 bytes BE)

    // Content item metadata (stored for round-trip writing)
    private int _metaFileType;
    private uint _metaSizeInBytes;
    private ulong _metaUniqueId;
    private ulong _metaParentUniqueId;
    private ulong _metaRootUniqueId;
    private ulong _metaGameId;
    private int _metaActivity;
    private int _metaGameMode;
    private uint _metaGameEngineType;
    private int _metaMegaloCategoryIndex;
    private ulong _metaCreationTime;
    private ulong _metaCreatorXuid;
    private bool _metaCreatorXuidIsOnline;
    private ulong _metaModificationTime;
    private ulong _metaModifierXuid;
    private string _metaModifierName = "";
    private bool _metaModifierXuidIsOnline;
    // Conditional metadata fields
    private int _metaFilmSeconds;
    private int _metaIconIndex;
    private uint _metaHopperIdentifier;
    private uint _metaCampaignId;
    private uint _metaCampaignDifficulty;
    private uint _metaCampaignScoring;
    private uint _metaCampaignInsertion;
    private uint _metaCampaignPrimarySkulls;
    private uint _metaCampaignSecondarySkulls;
    private uint _metaFirefightDifficulty;
    private uint _metaFirefightPrimary;
    private uint _metaFirefightSecondary;

    // Map variant header (stored for round-trip writing)
    private uint _hdrVariantVersion;
    private uint _hdrMapRsaHash;
    private uint _hdrScenarioPaletteCrc;
    private uint _hdrMapIdCopy;
    private bool _hdrBuiltIn;
    private bool _hdrBuiltFromXml;

    // String table (stored for round-trip writing)
    private int _stringCount;
    private (bool Exists, int Offset)[] _stringEntries = Array.Empty<(bool, int)>();
    private int _stringBufferSize;
    private bool _stringIsCompressed;
    private byte[] _stringBufferData = Array.Empty<byte>();

    public MccReachMapVariant(BlfFile blfFile)
    {
        _blfFile = blfFile;

        var mvar = blfFile.GetChunk("mvar")
            ?? throw new InvalidDataException("Missing mvar chunk.");

        byte[] payload = mvar.Data;

        // Store 24-byte prefix for round-trip writing
        Array.Copy(payload, 0, _prefix, 0, Math.Min(24, payload.Length));

        // Skip 24-byte prefix: SHA-1 hash (20 bytes) + packed variant length (4 bytes BE)
        var bits = new BitReader(payload);
        bits.SkipBits(24 * 8);

        ReadContentItemMetadata(bits);
        ReadMapVariantHeader(bits);
        ReadStringTable(bits);
        ReadVariantObjects(bits);
        ReadQuotas(bits);
        ResolvePaletteNames();
        LinkPlacements();
    }

    /// <summary>
    /// Reads s_content_item_metadata (Reach TU1/MCC layout).
    /// All fields are stored for round-trip writing.
    /// </summary>
    private void ReadContentItemMetadata(BitReader bits)
    {
        // file_type: 4-bit unsigned, stored as value+1
        _metaFileType = (int)bits.ReadInteger(4) - 1;

        // size_in_bytes: 32-bit unsigned
        _metaSizeInBytes = bits.ReadInteger(32);

        // unique_id, parent, root, game IDs: 64-bit each
        _metaUniqueId = bits.ReadInteger64(64);
        _metaParentUniqueId = bits.ReadInteger64(64);
        _metaRootUniqueId = bits.ReadInteger64(64);
        _metaGameId = bits.ReadInteger64(64);

        // activity: 3-bit unsigned, stored as value+1
        _metaActivity = (int)bits.ReadInteger(3) - 1;

        // game_mode: 3-bit
        _metaGameMode = (int)bits.ReadInteger(3);

        // game_engine_type: 3-bit
        _metaGameEngineType = bits.ReadInteger(3);

        // map_id: 32-bit signed
        MapId = bits.ReadSignedInteger(32);

        // megalo_category_index: 8-bit signed
        _metaMegaloCategoryIndex = bits.ReadSignedInteger(8);

        // creation_time: 64-bit
        _metaCreationTime = bits.ReadInteger64(64);

        // creator_xuid: 64-bit
        _metaCreatorXuid = bits.ReadInteger64(64);

        // creator_name: ASCII null-terminated, 16 bytes max
        MapAuthor = bits.ReadStringUtf8(16);

        // creator_xuid_is_online: 1 bit
        _metaCreatorXuidIsOnline = bits.ReadBool();

        // modification_time: 64-bit
        _metaModificationTime = bits.ReadInteger64(64);

        // modifier_xuid: 64-bit
        _metaModifierXuid = bits.ReadInteger64(64);

        // modifier_name: ASCII null-terminated, 16 bytes max
        _metaModifierName = bits.ReadStringUtf8(16);

        // modifier_xuid_is_online: 1 bit
        _metaModifierXuidIsOnline = bits.ReadBool();

        // name: wchar null-terminated, 128 wchars max
        VariantName = bits.ReadStringWchar(128);

        // description: wchar null-terminated, 128 wchars max
        VariantDescription = bits.ReadStringWchar(128);

        // Conditional fields based on file_type
        if (_metaFileType == 3 || _metaFileType == 4) // film
            _metaFilmSeconds = bits.ReadSignedInteger(32);
        else if (_metaFileType == 6) // game variant
            _metaIconIndex = bits.ReadSignedInteger(8);

        // Conditional based on activity
        if (_metaActivity == 2) // matchmaking
            _metaHopperIdentifier = bits.ReadInteger(16);

        // Conditional based on game_mode
        if (_metaGameMode == 1) // campaign
        {
            _metaCampaignId = bits.ReadInteger(8);
            _metaCampaignDifficulty = bits.ReadInteger(2);
            _metaCampaignScoring = bits.ReadInteger(2);
            _metaCampaignInsertion = bits.ReadInteger(8);
            _metaCampaignPrimarySkulls = bits.ReadInteger(16);
            _metaCampaignSecondarySkulls = bits.ReadInteger(16);
        }
        else if (_metaGameMode == 2) // firefight
        {
            _metaFirefightDifficulty = bits.ReadInteger(2);
            _metaFirefightPrimary = bits.ReadInteger(16);
            _metaFirefightSecondary = bits.ReadInteger(16);
        }
    }

    /// <summary>
    /// Reads c_map_variant header (Reach TU1/MCC layout).
    /// All fields are stored for round-trip writing.
    /// </summary>
    private void ReadMapVariantHeader(BitReader bits)
    {
        _hdrVariantVersion = bits.ReadInteger(8);
        _hdrMapRsaHash = bits.ReadInteger(32);
        _hdrScenarioPaletteCrc = bits.ReadInteger(32);

        _numberOfQuotas = (int)bits.ReadInteger(9);

        _hdrMapIdCopy = bits.ReadInteger(32);
        _hdrBuiltIn = bits.ReadBool();
        _hdrBuiltFromXml = bits.ReadBool();

        // World bounds: 6 × 32-bit raw floats
        WorldBoundsXMin = bits.ReadRawFloat();
        WorldBoundsXMax = bits.ReadRawFloat();
        WorldBoundsYMin = bits.ReadRawFloat();
        WorldBoundsYMax = bits.ReadRawFloat();
        WorldBoundsZMin = bits.ReadRawFloat();
        WorldBoundsZMax = bits.ReadRawFloat();

        // Budgets: 32-bit unsigned integers (not floats like H3)
        MaximumBudget = bits.ReadInteger(32);
        CurrentBudget = bits.ReadInteger(32);
    }

    /// <summary>
    /// Reads forge label string table (c_single_language_string_table).
    /// All fields are stored for round-trip writing.
    /// </summary>
    private void ReadStringTable(BitReader bits)
    {
        _stringCount = (int)bits.ReadInteger(9);

        // Per-string: 1-bit exists flag + optional 12-bit offset
        _stringEntries = new (bool, int)[_stringCount];
        for (int i = 0; i < _stringCount; i++)
        {
            bool exists = bits.ReadBool();
            int offset = 0;
            if (exists)
                offset = (int)bits.ReadInteger(12);
            _stringEntries[i] = (exists, offset);
        }

        // Buffer data (only if strings exist)
        if (_stringCount > 0)
        {
            _stringBufferSize = (int)bits.ReadInteger(13);
            _stringIsCompressed = bits.ReadBool();

            if (_stringIsCompressed)
            {
                int compressedSize = (int)bits.ReadInteger(13);
                _stringBufferData = bits.ReadRawData(compressedSize);
            }
            else
            {
                _stringBufferData = bits.ReadRawData(_stringBufferSize);
            }
        }
    }

    /// <summary>
    /// Reads 651 variant objects with Reach's adaptive position and 20-bit orientation encoding.
    /// </summary>
    private void ReadVariantObjects(BitReader bits)
    {
        var (bitsX, bitsY, bitsZ) = ReachPositionEncoding.ComputeAxisBitCounts(
            WorldBoundsXMin, WorldBoundsXMax,
            WorldBoundsYMin, WorldBoundsYMax,
            WorldBoundsZMin, WorldBoundsZMax);

        PlacementChunks = new List<PlacementChunk>(VariantObjectCount);

        for (int i = 0; i < VariantObjectCount; i++)
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

            // flags: 2-bit
            placement.PackedFlags = (ushort)bits.ReadInteger(2);

            // variant_quota_index: index encoding (1-bit absent + 8-bit value)
            placement.TagsIndex = ReadIndexEncoded(bits, 8);

            // variant_index: index encoding (1-bit absent + 5-bit value)
            placement.VariantIndex = ReadIndexEncoded(bits, 5);

            // Position
            bool pointInBounds = bits.ReadBool();
            float px, py, pz;
            if (pointInBounds)
            {
                px = ReachPositionEncoding.DecodePosition(bits, bitsX, WorldBoundsXMin, WorldBoundsXMax);
                py = ReachPositionEncoding.DecodePosition(bits, bitsY, WorldBoundsYMin, WorldBoundsYMax);
                pz = ReachPositionEncoding.DecodePosition(bits, bitsZ, WorldBoundsZMin, WorldBoundsZMax);
            }
            else
            {
                // Fallback for objects outside world bounds (shouldn't occur in saved files)
                px = bits.ReadRawFloat();
                py = bits.ReadRawFloat();
                pz = bits.ReadRawFloat();
            }

            // Orientation: 20-bit axis + 14-bit angle
            var (fi, fj, fk, ui, uj, uk) = ReachOrientationConverter.ReadOrientation(bits);
            var (yaw, pitch, roll) = OrientationConverter.ToYawPitchRoll(fi, fj, fk, ui, uj, uk);

            placement.SpawnCoords = new SpawnCoords
            {
                X = px, Y = py, Z = pz,
                Yaw = yaw, Pitch = pitch, Roll = roll
            };

            // spawn_relative_to: 10-bit unsigned, stored as value+1
            placement.SpawnRelativeTo = (int)bits.ReadInteger(10) - 1;

            // Multiplayer object properties
            ReadMultiplayerObjectProperties(bits, placement);

            placement.HasPosition = true;
            placement.ChunkType = ChunkType.Added;
            PlacementChunks.Add(placement);
        }

        SpawnedObjectCount = (byte)PlacementChunks.Count(p => p.TagsIndex >= 0);
    }

    /// <summary>
    /// Reads s_variant_multiplayer_object_properties_definition.
    /// All fields are stored for round-trip writing.
    /// </summary>
    private void ReadMultiplayerObjectProperties(BitReader bits, PlacementChunk placement)
    {
        // boundary.shape: 2-bit (0=unused, 1=sphere, 2=cylinder, 3=box)
        placement.BoundaryShape = (byte)bits.ReadInteger(2);

        switch (placement.BoundaryShape)
        {
            case 1: // sphere
                placement.BoundarySize = bits.ReadQuantizedReal(11, 0f, 200f, false);
                break;
            case 2: // cylinder
                placement.BoundarySize = bits.ReadQuantizedReal(11, 0f, 200f, false);
                placement.BoundaryPositiveHeight = bits.ReadQuantizedReal(11, 0f, 200f, false);
                placement.BoundaryNegativeHeight = bits.ReadQuantizedReal(11, 0f, 200f, false);
                break;
            case 3: // box
                placement.BoundarySize = bits.ReadQuantizedReal(11, 0f, 200f, false);
                placement.BoundaryBoxLength = bits.ReadQuantizedReal(11, 0f, 200f, false);
                placement.BoundaryPositiveHeight = bits.ReadQuantizedReal(11, 0f, 200f, false);
                placement.BoundaryNegativeHeight = bits.ReadQuantizedReal(11, 0f, 200f, false);
                break;
        }

        // user_data (spawn_sequence): 8-bit
        placement.SpawnSequence = (byte)bits.ReadInteger(8);

        // spawn_time: 8-bit
        placement.RespawnTime = (byte)bits.ReadInteger(8);

        // cached_type: 5-bit
        placement.ObjectType = (int)bits.ReadInteger(5);

        // label_index: index encoding (1-bit absent + 8-bit value)
        placement.LabelIndex = ReadIndexEncoded(bits, 8);

        // placement_flags: 8-bit
        placement.Flags = (byte)bits.ReadInteger(8);

        // team: 4-bit unsigned, stored as value+1 (-1 = neutral)
        placement.ReachTeamRaw = bits.ReadInteger(4);
        int team = (int)placement.ReachTeamRaw - 1;
        placement.Team = (byte)Math.Max(0, team);

        // primary_change_color_index: index encoding (1-bit absent + 3-bit)
        placement.PrimaryColorIndex = ReadIndexEncoded(bits, 3);

        // Conditional fields based on cached_type
        switch (placement.ObjectType)
        {
            case 1: // weapon
                placement.SpareClips = (byte)bits.ReadInteger(8);
                break;
            case 12: // teleporter_receiver
            case 13: // teleporter_sender
            case 14: // teleporter_2way
                placement.TeleporterChannel = (byte)bits.ReadInteger(5);
                placement.TeleporterPassability = (byte)bits.ReadInteger(5);
                break;
            case 19: // location_name
                placement.LocationNameIndex = ReadIndexEncoded(bits, 8);
                break;
        }
    }

    /// <summary>
    /// Reads Reach quotas: 3 × 8-bit per entry (min_count, max_count, placed_on_map).
    /// No object_definition_index or cost — synthetic Tag objects are created for UI display.
    /// </summary>
    private void ReadQuotas(BitReader bits)
    {
        TagIndex = new List<TagIndexEntry>(MaxQuotas);

        int count = Math.Min(MaxQuotas, _numberOfQuotas);
        for (int i = 0; i < count; i++)
        {
            var entry = new TagIndexEntry();
            entry.Offset = i;
            entry.Ident = i; // Use index as synthetic ident

            entry.RunTimeMinimum = (byte)bits.ReadInteger(8);
            entry.RunTimeMaximum = (byte)bits.ReadInteger(8);
            entry.CountOnMap = (byte)bits.ReadInteger(8);

            // Synthetic tag for UI display (Reach has no tag database)
            entry.Tag = new Tag
            {
                Ident = i,
                Path = $"Quota #{i}",
                Class = "reach_object",
                TagsIndex = i
            };

            TagIndex.Add(entry);
        }

        // Fill remaining slots
        for (int i = TagIndex.Count; i < MaxQuotas; i++)
        {
            TagIndex.Add(new TagIndexEntry
            {
                Offset = i,
                Ident = -1
            });
        }
    }

    /// <summary>
    /// Resolves quota names from the Reach palette database.
    /// Replaces synthetic "Quota #N" paths with real object names where available.
    /// </summary>
    private void ResolvePaletteNames()
    {
        var palette = ReachPaletteDatabase.Create(MapId);
        if (palette == null) return;

        Palette = palette;

        for (int i = 0; i < TagIndex.Count; i++)
        {
            var entry = TagIndex[i];
            if (entry.Tag == null) continue;

            var name = palette.GetQuotaName(i);
            if (name != null)
            {
                entry.Tag.Path = name;
                entry.Tag.Class = palette.GetCategory(i) ?? "reach_object";
            }
            else if (entry.CountOnMap == 0 && entry.RunTimeMaximum == 0)
            {
                // Empty padding slot beyond the palette - hide from UI
                entry.Tag = null;
                entry.Ident = -1;
            }
        }
    }

    // ======== Write methods ========

    /// <summary>
    /// Writes the entire Reach packed mvar bitstream and saves to the BLF file.
    /// </summary>
    public void SaveAll()
    {
        var bits = new BitWriter();
        WriteContentItemMetadata(bits);
        WriteMapVariantHeader(bits);
        WriteStringTable(bits);
        WriteVariantObjects(bits);
        WriteQuotas(bits);

        byte[] payload = bits.ToArray();

        // Build full mvar chunk data: 24-byte prefix + packed data
        byte[] fullData = new byte[24 + payload.Length];
        Array.Copy(_prefix, 0, fullData, 0, 20); // original SHA-1 hash
        // Update packed length (4 bytes BE)
        int len = payload.Length;
        fullData[20] = (byte)(len >> 24);
        fullData[21] = (byte)(len >> 16);
        fullData[22] = (byte)(len >> 8);
        fullData[23] = (byte)len;
        Array.Copy(payload, 0, fullData, 24, payload.Length);

        _blfFile.UpdateChunkData("mvar", fullData);
        _blfFile.Write();
    }

    /// <summary>
    /// No-op: SaveAll() handles all writing in one pass for packed format.
    /// </summary>
    public void WriteHeader() { }

    /// <summary>
    /// No-op: SaveAll() handles all writing in one pass for packed format.
    /// </summary>
    public void WritePlacement(PlacementChunk chunk) { }

    /// <summary>
    /// No-op: SaveAll() handles all writing in one pass for packed format.
    /// </summary>
    public void WriteTagIndexEntry(TagIndexEntry entry) { }

    private void WriteContentItemMetadata(BitWriter bits)
    {
        bits.WriteInteger((uint)(_metaFileType + 1), 4);
        bits.WriteInteger(_metaSizeInBytes, 32);
        bits.WriteInteger64(_metaUniqueId, 64);
        bits.WriteInteger64(_metaParentUniqueId, 64);
        bits.WriteInteger64(_metaRootUniqueId, 64);
        bits.WriteInteger64(_metaGameId, 64);
        bits.WriteInteger((uint)(_metaActivity + 1), 3);
        bits.WriteInteger((uint)_metaGameMode, 3);
        bits.WriteInteger(_metaGameEngineType, 3);
        bits.WriteSignedInteger(MapId, 32);
        bits.WriteSignedInteger(_metaMegaloCategoryIndex, 8);
        bits.WriteInteger64(_metaCreationTime, 64);
        bits.WriteInteger64(_metaCreatorXuid, 64);
        bits.WriteStringUtf8(MapAuthor, 16);
        bits.WriteBool(_metaCreatorXuidIsOnline);
        bits.WriteInteger64(_metaModificationTime, 64);
        bits.WriteInteger64(_metaModifierXuid, 64);
        bits.WriteStringUtf8(_metaModifierName, 16);
        bits.WriteBool(_metaModifierXuidIsOnline);
        bits.WriteStringWchar(VariantName, 128);
        bits.WriteStringWchar(VariantDescription, 128);

        if (_metaFileType == 3 || _metaFileType == 4)
            bits.WriteSignedInteger(_metaFilmSeconds, 32);
        else if (_metaFileType == 6)
            bits.WriteSignedInteger(_metaIconIndex, 8);

        if (_metaActivity == 2)
            bits.WriteInteger(_metaHopperIdentifier, 16);

        if (_metaGameMode == 1)
        {
            bits.WriteInteger(_metaCampaignId, 8);
            bits.WriteInteger(_metaCampaignDifficulty, 2);
            bits.WriteInteger(_metaCampaignScoring, 2);
            bits.WriteInteger(_metaCampaignInsertion, 8);
            bits.WriteInteger(_metaCampaignPrimarySkulls, 16);
            bits.WriteInteger(_metaCampaignSecondarySkulls, 16);
        }
        else if (_metaGameMode == 2)
        {
            bits.WriteInteger(_metaFirefightDifficulty, 2);
            bits.WriteInteger(_metaFirefightPrimary, 16);
            bits.WriteInteger(_metaFirefightSecondary, 16);
        }
    }

    private void WriteMapVariantHeader(BitWriter bits)
    {
        bits.WriteInteger(_hdrVariantVersion, 8);
        bits.WriteInteger(_hdrMapRsaHash, 32);
        bits.WriteInteger(_hdrScenarioPaletteCrc, 32);
        bits.WriteInteger((uint)_numberOfQuotas, 9);
        bits.WriteInteger(_hdrMapIdCopy, 32);
        bits.WriteBool(_hdrBuiltIn);
        bits.WriteBool(_hdrBuiltFromXml);

        bits.WriteRawFloat(WorldBoundsXMin);
        bits.WriteRawFloat(WorldBoundsXMax);
        bits.WriteRawFloat(WorldBoundsYMin);
        bits.WriteRawFloat(WorldBoundsYMax);
        bits.WriteRawFloat(WorldBoundsZMin);
        bits.WriteRawFloat(WorldBoundsZMax);

        // Budgets: 32-bit unsigned integers
        bits.WriteInteger((uint)MaximumBudget, 32);
        bits.WriteInteger((uint)CurrentBudget, 32);
    }

    private void WriteStringTable(BitWriter bits)
    {
        bits.WriteInteger((uint)_stringCount, 9);

        for (int i = 0; i < _stringCount; i++)
        {
            var (exists, offset) = _stringEntries[i];
            bits.WriteBool(exists);
            if (exists)
                bits.WriteInteger((uint)offset, 12);
        }

        if (_stringCount > 0)
        {
            bits.WriteInteger((uint)_stringBufferSize, 13);
            bits.WriteBool(_stringIsCompressed);

            if (_stringIsCompressed)
            {
                bits.WriteInteger((uint)_stringBufferData.Length, 13);
            }

            bits.WriteRawData(_stringBufferData);
        }
    }

    private void WriteVariantObjects(BitWriter bits)
    {
        var (bitsX, bitsY, bitsZ) = ReachPositionEncoding.ComputeAxisBitCounts(
            WorldBoundsXMin, WorldBoundsXMax,
            WorldBoundsYMin, WorldBoundsYMax,
            WorldBoundsZMin, WorldBoundsZMax);

        for (int i = 0; i < VariantObjectCount; i++)
        {
            var placement = i < PlacementChunks.Count ? PlacementChunks[i] : null;
            bool exists = placement != null && placement.TagsIndex >= 0;

            bits.WriteBool(exists);
            if (!exists)
                continue;

            // flags: 2-bit
            bits.WriteInteger(placement!.PackedFlags, 2);

            // variant_quota_index: index encoding
            WriteIndexEncoded(bits, placement.TagsIndex, 8);

            // variant_index: index encoding
            WriteIndexEncoded(bits, placement.VariantIndex, 5);

            // Position: always write as in-bounds (quantized)
            bits.WriteBool(true); // point_in_bounds = true

            var coords = placement.SpawnCoords;
            bits.WriteInteger(
                ReachPositionEncoding.EncodePosition(coords.X, bitsX, WorldBoundsXMin, WorldBoundsXMax), bitsX);
            bits.WriteInteger(
                ReachPositionEncoding.EncodePosition(coords.Y, bitsY, WorldBoundsYMin, WorldBoundsYMax), bitsY);
            bits.WriteInteger(
                ReachPositionEncoding.EncodePosition(coords.Z, bitsZ, WorldBoundsZMin, WorldBoundsZMax), bitsZ);

            // Orientation: convert yaw/pitch/roll to forward/up, then encode
            var (fi, fj, fk, ui, uj, uk) = OrientationConverter.ToForwardUp(
                coords.Yaw, coords.Pitch, coords.Roll);
            ReachOrientationConverter.WriteOrientation(bits, fi, fj, fk, ui, uj, uk);

            // spawn_relative_to: 10-bit, stored as value+1
            bits.WriteInteger((uint)(placement.SpawnRelativeTo + 1), 10);

            // Multiplayer object properties
            WriteMultiplayerObjectProperties(bits, placement);
        }
    }

    private void WriteMultiplayerObjectProperties(BitWriter bits, PlacementChunk placement)
    {
        bits.WriteInteger(placement.BoundaryShape, 2);

        switch (placement.BoundaryShape)
        {
            case 1: // sphere
                bits.WriteQuantizedReal(placement.BoundarySize, 11, 0f, 200f, false);
                break;
            case 2: // cylinder
                bits.WriteQuantizedReal(placement.BoundarySize, 11, 0f, 200f, false);
                bits.WriteQuantizedReal(placement.BoundaryPositiveHeight, 11, 0f, 200f, false);
                bits.WriteQuantizedReal(placement.BoundaryNegativeHeight, 11, 0f, 200f, false);
                break;
            case 3: // box
                bits.WriteQuantizedReal(placement.BoundarySize, 11, 0f, 200f, false);
                bits.WriteQuantizedReal(placement.BoundaryBoxLength, 11, 0f, 200f, false);
                bits.WriteQuantizedReal(placement.BoundaryPositiveHeight, 11, 0f, 200f, false);
                bits.WriteQuantizedReal(placement.BoundaryNegativeHeight, 11, 0f, 200f, false);
                break;
        }

        bits.WriteInteger(placement.SpawnSequence, 8);
        bits.WriteInteger(placement.RespawnTime, 8);
        bits.WriteInteger((uint)placement.ObjectType, 5);
        WriteIndexEncoded(bits, placement.LabelIndex, 8);
        bits.WriteInteger(placement.Flags, 8);

        // team: 4-bit, stored as value+1 (use raw value for round-trip fidelity)
        bits.WriteInteger(placement.ReachTeamRaw, 4);

        WriteIndexEncoded(bits, placement.PrimaryColorIndex, 3);

        switch (placement.ObjectType)
        {
            case 1: // weapon
                bits.WriteInteger(placement.SpareClips, 8);
                break;
            case 12: // teleporter_receiver
            case 13: // teleporter_sender
            case 14: // teleporter_2way
                bits.WriteInteger(placement.TeleporterChannel, 5);
                bits.WriteInteger(placement.TeleporterPassability, 5);
                break;
            case 19: // location_name
                WriteIndexEncoded(bits, placement.LocationNameIndex, 8);
                break;
        }
    }

    private void WriteQuotas(BitWriter bits)
    {
        int count = Math.Min(MaxQuotas, _numberOfQuotas);
        for (int i = 0; i < count; i++)
        {
            var entry = i < TagIndex.Count ? TagIndex[i] : new TagIndexEntry();
            bits.WriteInteger(entry.RunTimeMinimum, 8);
            bits.WriteInteger(entry.RunTimeMaximum, 8);
            bits.WriteInteger(entry.CountOnMap, 8);
        }
    }

    // ======== Helper methods ========

    /// <summary>
    /// Reads a Reach index-encoded value: 1-bit absent flag, then N-bit unsigned value.
    /// Returns -1 if absent, otherwise the N-bit value.
    /// </summary>
    private static int ReadIndexEncoded(BitReader bits, int valueBits)
    {
        bool absent = bits.ReadBool();
        if (absent)
            return -1;
        return (int)bits.ReadInteger(valueBits);
    }

    /// <summary>
    /// Writes a Reach index-encoded value: 1-bit absent flag, then N-bit unsigned value.
    /// </summary>
    private static void WriteIndexEncoded(BitWriter bits, int value, int valueBits)
    {
        if (value < 0)
        {
            bits.WriteBool(true); // absent
        }
        else
        {
            bits.WriteBool(false);
            bits.WriteInteger((uint)value, valueBits);
        }
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
        // No persistent streams to close
    }
}
