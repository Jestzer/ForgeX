using ForgeX.Core.IO;
using ForgeX.Core.Halo3;
using ForgeX.Core.Blf;
using ForgeX.Core.Reach;

namespace ForgeX.Core.Halo4;

/// <summary>
/// Reads Halo 4 MCC .mvar files (BLF packed mvar, version 50).
/// H4 shares the Reach bitstream foundation (adaptive position, 20-bit orientation,
/// 651 object slots, integer budgets) but has key differences:
/// - Content metadata: activity is 2-bit unsigned (not 3-bit value+1)
/// - Per-object: 6-bit type (not 5), scale(6), isLocked(1), unk10(10), 4 label slots
/// - Position: always adaptive regardless of point_in_bounds flag
/// - Field order differs from Reach within multiplayer properties
/// - Variable-size intermediate section between objects and quotas
/// </summary>
public class MccHalo4MapVariant : IMapVariantData
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
    public Halo4PaletteDatabase? Palette { get; private set; }
    public bool CanWrite => false; // Read-only until write path is verified

    private int _numberOfQuotas;

    // BLF container reference
    private BlfFile _blfFile;
    private byte[] _prefix = new byte[24]; // SHA-1 hash (20 bytes) + packed length (4 bytes BE)
    private byte[] _rawPayload = Array.Empty<byte>(); // Full packed bitstream for quota search

    // Content item metadata (stored for future round-trip writing)
    private int _metaFileType;
    private uint _metaSizeInBytes;
    private ulong _metaUniqueId;
    private ulong _metaParentUniqueId;
    private ulong _metaRootUniqueId;
    private ulong _metaGameId;
    private uint _metaActivity; // 2-bit unsigned (NOT value+1 like Reach)
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

    // Map variant header
    private uint _hdrVariantVersion;
    private uint _hdrMapRsaHash;
    private uint _hdrScenarioPaletteCrc;
    private uint _hdrMapIdCopy;
    private bool _hdrBuiltIn;
    private bool _hdrBuiltFromXml;

    // String table
    private int _stringCount;
    private (bool Exists, int Offset)[] _stringEntries = Array.Empty<(bool, int)>();
    private int _stringBufferSize;
    private bool _stringIsCompressed;
    private byte[] _stringBufferData = Array.Empty<byte>();

    // Opaque data sections for future round-trip writing
    private byte[] _intermediateData = Array.Empty<byte>(); // Between objects and quotas
    private int _intermediateBitCount;
    private byte[] _postQuotaData = Array.Empty<byte>();    // After quotas to end of packed data
    private int _postQuotaBitCount;

    public MccHalo4MapVariant(BlfFile blfFile)
    {
        _blfFile = blfFile;

        var mvar = blfFile.GetChunk("mvar")
            ?? throw new InvalidDataException("Missing mvar chunk.");

        byte[] payload = mvar.Data;

        // Store 24-byte prefix for future round-trip writing
        Array.Copy(payload, 0, _prefix, 0, Math.Min(24, payload.Length));

        // Read packed variant length from prefix (4 bytes BE at offset 20)
        int packedLenBytes = (payload[20] << 24) | (payload[21] << 16) |
                             (payload[22] << 8) | payload[23];
        int packedBits = packedLenBytes * 8;

        // Store raw payload for quota search
        _rawPayload = payload;

        var bits = new BitReader(payload);
        bits.SkipBits(24 * 8); // Skip 24-byte prefix

        ReadContentItemMetadata(bits);
        ReadMapVariantHeader(bits);
        ReadStringTable(bits);

        int objStartBit = bits.BitOffset;
        ReadVariantObjects(bits);
        int objEndBit = bits.BitOffset;

        // Find and read quotas (handles variable-size intermediate section)
        int quotaStartBit = FindQuotaStart(payload, objEndBit, packedBits + 24 * 8);
        if (quotaStartBit >= 0)
        {
            // Store intermediate section as opaque data
            _intermediateBitCount = quotaStartBit - objEndBit;
            if (_intermediateBitCount > 0)
            {
                var intermBits = new BitReader(payload);
                intermBits.SkipBits(objEndBit);
                _intermediateData = intermBits.ReadRawData((_intermediateBitCount + 7) / 8);
            }

            // Read quotas
            bits = new BitReader(payload);
            bits.SkipBits(quotaStartBit);
            ReadQuotas(bits);

            // Store post-quota section as opaque data
            int quotaEndBit = quotaStartBit + _numberOfQuotas * 24;
            int packedEndBit = 24 * 8 + packedBits;
            _postQuotaBitCount = packedEndBit - quotaEndBit;
            if (_postQuotaBitCount > 0)
            {
                var postBits = new BitReader(payload);
                postBits.SkipBits(quotaEndBit);
                _postQuotaData = postBits.ReadRawData((_postQuotaBitCount + 7) / 8);
            }
        }
        else
        {
            // Fallback: read quotas directly after objects (may be incorrect for some maps)
            ReadQuotas(bits);
        }

        // Release raw payload reference (no longer needed)
        _rawPayload = Array.Empty<byte>();

        LinkPlacements();
        ResolvePaletteNames();
    }

    /// <summary>
    /// Reads s_content_item_metadata (H4 MCC layout).
    /// Same as Reach except activity is 2-bit unsigned (not 3-bit value+1).
    /// </summary>
    private void ReadContentItemMetadata(BitReader bits)
    {
        // file_type: 4-bit unsigned, stored as value+1
        _metaFileType = (int)bits.ReadInteger(4) - 1;

        _metaSizeInBytes = bits.ReadInteger(32);
        _metaUniqueId = bits.ReadInteger64(64);
        _metaParentUniqueId = bits.ReadInteger64(64);
        _metaRootUniqueId = bits.ReadInteger64(64);
        _metaGameId = bits.ReadInteger64(64);

        // H4 difference: activity is 2-bit unsigned (NOT value+1 like Reach's 3-bit)
        _metaActivity = bits.ReadInteger(2);

        _metaGameMode = (int)bits.ReadInteger(3);
        _metaGameEngineType = bits.ReadInteger(3);
        MapId = bits.ReadSignedInteger(32);
        _metaMegaloCategoryIndex = bits.ReadSignedInteger(8);
        _metaCreationTime = bits.ReadInteger64(64);
        _metaCreatorXuid = bits.ReadInteger64(64);
        MapAuthor = bits.ReadStringUtf8(16);
        _metaCreatorXuidIsOnline = bits.ReadBool();
        _metaModificationTime = bits.ReadInteger64(64);
        _metaModifierXuid = bits.ReadInteger64(64);
        _metaModifierName = bits.ReadStringUtf8(16);
        _metaModifierXuidIsOnline = bits.ReadBool();
        VariantName = bits.ReadStringWchar(128);
        VariantDescription = bits.ReadStringWchar(128);

        // Conditional fields based on file_type (same as Reach)
        if (_metaFileType == 3 || _metaFileType == 4)
            _metaFilmSeconds = bits.ReadSignedInteger(32);
        else if (_metaFileType == 6)
            _metaIconIndex = bits.ReadSignedInteger(8);

        // Conditional based on activity
        if (_metaActivity == 2)
            _metaHopperIdentifier = bits.ReadInteger(16);

        // Conditional based on game_mode (same as Reach)
        if (_metaGameMode == 1)
        {
            _metaCampaignId = bits.ReadInteger(8);
            _metaCampaignDifficulty = bits.ReadInteger(2);
            _metaCampaignScoring = bits.ReadInteger(2);
            _metaCampaignInsertion = bits.ReadInteger(8);
            _metaCampaignPrimarySkulls = bits.ReadInteger(16);
            _metaCampaignSecondarySkulls = bits.ReadInteger(16);
        }
        else if (_metaGameMode == 2)
        {
            _metaFirefightDifficulty = bits.ReadInteger(2);
            _metaFirefightPrimary = bits.ReadInteger(16);
            _metaFirefightSecondary = bits.ReadInteger(16);
        }
    }

    /// <summary>
    /// Reads c_map_variant header (same structure as Reach).
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

        WorldBoundsXMin = bits.ReadRawFloat();
        WorldBoundsXMax = bits.ReadRawFloat();
        WorldBoundsYMin = bits.ReadRawFloat();
        WorldBoundsYMax = bits.ReadRawFloat();
        WorldBoundsZMin = bits.ReadRawFloat();
        WorldBoundsZMax = bits.ReadRawFloat();

        MaximumBudget = bits.ReadInteger(32);
        CurrentBudget = bits.ReadInteger(32);
    }

    /// <summary>
    /// Reads forge label string table (same structure as Reach).
    /// </summary>
    private void ReadStringTable(BitReader bits)
    {
        _stringCount = (int)bits.ReadInteger(9);

        _stringEntries = new (bool, int)[_stringCount];
        for (int i = 0; i < _stringCount; i++)
        {
            bool exists = bits.ReadBool();
            int offset = 0;
            if (exists)
                offset = (int)bits.ReadInteger(12);
            _stringEntries[i] = (exists, offset);
        }

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
    /// Reads 651 variant objects with H4's modified Reach encoding.
    /// Key differences: always-adaptive position, 6-bit type, scale/locked/unk10 fields,
    /// reordered multiplayer properties, 4 label slots.
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

            // Position: point_in_bounds flag + ALWAYS adaptive encoding (H4 difference)
            bool pointInBounds = bits.ReadBool();
            float px = ReachPositionEncoding.DecodePosition(bits, bitsX, WorldBoundsXMin, WorldBoundsXMax);
            float py = ReachPositionEncoding.DecodePosition(bits, bitsY, WorldBoundsYMin, WorldBoundsYMax);
            float pz = ReachPositionEncoding.DecodePosition(bits, bitsZ, WorldBoundsZMin, WorldBoundsZMax);
            placement.HasPosition = pointInBounds;

            // Orientation: 20-bit axis + 14-bit angle (same as Reach)
            var (fi, fj, fk, ui, uj, uk) = ReachOrientationConverter.ReadOrientation(bits);
            var (yaw, pitch, roll) = OrientationConverter.ToYawPitchRoll(fi, fj, fk, ui, uj, uk);

            placement.SpawnCoords = new SpawnCoords
            {
                X = px, Y = py, Z = pz,
                Yaw = yaw, Pitch = pitch, Roll = roll
            };

            // spawn_relative_to: 10-bit unsigned, stored as value+1
            placement.SpawnRelativeTo = (int)bits.ReadInteger(10) - 1;

            // H4-specific: scale (6-bit) and isLocked (1-bit)
            placement.H4ScaleRaw = (byte)bits.ReadInteger(6);
            placement.H4IsLocked = bits.ReadBool();

            // Multiplayer object properties (H4 field order)
            ReadMultiplayerObjectProperties(bits, placement);

            placement.ChunkType = ChunkType.Added;
            PlacementChunks.Add(placement);
        }

        SpawnedObjectCount = (byte)PlacementChunks.Count(p => p.TagsIndex >= 0);
    }

    /// <summary>
    /// Reads H4 multiplayer object properties.
    /// Field order differs from Reach: shape, type, unk10, team, respawn, color, flags, seq, 4×labels, type_conditionals.
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

        // H4: object_type is 6-bit (vs 5-bit in Reach)
        placement.ObjectType = (int)bits.ReadInteger(6);

        // H4-specific: unk10 (10-bit unknown field)
        placement.H4Unk10 = (ushort)bits.ReadInteger(10);

        // team: 4-bit unsigned, stored as value+1 (-1 = neutral)
        placement.ReachTeamRaw = bits.ReadInteger(4);
        int team = (int)placement.ReachTeamRaw - 1;
        placement.Team = (byte)Math.Max(0, team);

        // spawn_time: 8-bit
        placement.RespawnTime = (byte)bits.ReadInteger(8);

        // primary_change_color_index: index encoding (1-bit absent + 3-bit)
        placement.PrimaryColorIndex = ReadIndexEncoded(bits, 3);

        // placement_flags: 8-bit
        placement.Flags = (byte)bits.ReadInteger(8);

        // spawn_sequence: 8-bit
        placement.SpawnSequence = (byte)bits.ReadInteger(8);

        // 4 label slots: each index encoding (1-bit absent + 8-bit)
        placement.LabelIndex = ReadIndexEncoded(bits, 8);
        placement.LabelIndex2 = ReadIndexEncoded(bits, 8);
        placement.LabelIndex3 = ReadIndexEncoded(bits, 8);
        placement.LabelIndex4 = ReadIndexEncoded(bits, 8);

        // Type-specific conditionals (from Nitrogen ObjectType enum)
        ReadTypeConditionals(bits, placement);
    }

    /// <summary>
    /// Reads type-specific conditional fields based on the object type.
    /// H4 has more type categories than Reach (types 0-35).
    /// </summary>
    private void ReadTypeConditionals(BitReader bits, PlacementChunk placement)
    {
        int type = placement.ObjectType;

        if (type == 1) // Weapon
        {
            placement.SpareClips = (byte)bits.ReadInteger(8);
        }

        if (type == 0 || (type >= 2 && type <= 6)) // SimpleObject types
        {
            // Two 5-bit unknown fields (similar structure to teleporter)
            var data = new byte[2];
            data[0] = (byte)bits.ReadInteger(5);
            data[1] = (byte)bits.ReadInteger(5);
            placement.H4TypeConditionalData = data;
        }

        if (type >= 13 && type <= 15) // Teleporter types
        {
            placement.TeleporterChannel = (byte)bits.ReadInteger(5);
            placement.TeleporterPassability = (byte)bits.ReadInteger(5);
        }

        if (type == 20) // NamedLocation
        {
            // 9-bit StreamPlusOne (value stored as value+1)
            int raw = (int)bits.ReadInteger(9);
            placement.LocationNameIndex = raw - 1;
        }

        if (type == 12) // Dispenser (VehiclePad)
        {
            var data = new byte[1];
            data[0] = (byte)bits.ReadInteger(8);
            placement.H4TypeConditionalData = data;
        }

        if (type == 31) // TraitZone
        {
            var data = new byte[1];
            data[0] = (byte)bits.ReadInteger(5);
            placement.H4TypeConditionalData = data;
        }

        if (type >= 21 && type <= 27) // SpecialObject types
        {
            var data = new byte[2];
            data[0] = (byte)bits.ReadInteger(5);
            data[1] = (byte)bits.ReadInteger(5);
            placement.H4TypeConditionalData = data;
        }

        if (type == 32) // InitialOrdnanceDrop
        {
            // 5 + 8 + 16 = 29 bits → store as 4 bytes
            var data = new byte[4];
            data[0] = (byte)bits.ReadInteger(5);
            data[1] = (byte)bits.ReadInteger(8);
            ushort val16 = (ushort)bits.ReadInteger(16);
            data[2] = (byte)(val16 >> 8);
            data[3] = (byte)(val16 & 0xFF);
            placement.H4TypeConditionalData = data;
        }

        if (type == 33) // RandomOrdnanceDrop
        {
            var data = new byte[8];
            for (int i = 0; i < 8; i++)
                data[i] = (byte)bits.ReadInteger(8);
            placement.H4TypeConditionalData = data;
        }

        if (type == 34) // ObjectiveOrdnanceDrop
        {
            var data = new byte[9];
            for (int i = 0; i < 9; i++)
                data[i] = (byte)bits.ReadInteger(8);
            placement.H4TypeConditionalData = data;
        }

        // Type 35 (PersonalOrdnanceDrop): no additional data
    }

    /// <summary>
    /// Finds the start bit position of the quota section using structural validity scoring.
    /// H4 has a variable-size intermediate section between objects and quotas.
    /// Scores candidate positions by checking that quota entries at used qi indices are
    /// structurally valid (min &lt;= max, placed &lt;= max) — at wrong bit positions,
    /// field misalignment causes detectable structural violations.
    /// </summary>
    private int FindQuotaStart(byte[] payload, int objEndBit, int packedEndBit)
    {
        if (_numberOfQuotas <= 0) return objEndBit;

        int quotaTotalBits = _numberOfQuotas * 24;
        if (packedEndBit - objEndBit < quotaTotalBits) return -1;

        // Collect distinct qi values and counts from parsed objects
        var qiCounts = new Dictionary<int, int>();
        foreach (var p in PlacementChunks)
        {
            if (p.TagsIndex >= 0 && p.TagsIndex < _numberOfQuotas)
                qiCounts[p.TagsIndex] = qiCounts.GetValueOrDefault(p.TagsIndex) + 1;
        }

        if (qiCounts.Count == 0)
            return objEndBit;

        int maxSearchEnd = packedEndBit - quotaTotalBits;
        int bestCandidate = -1;
        int bestScore = int.MinValue;

        // Forward search: covers small intermediate sections (0-2000 bits from objEnd)
        int forwardLimit = Math.Min(objEndBit + 2000, maxSearchEnd);
        for (int candidate = objEndBit; candidate <= forwardLimit; candidate++)
        {
            int score = ScoreQuotaPosition(payload, candidate, qiCounts);
            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
            }
        }

        // Backward search: covers variable post-quota sizes (0-5000 bits from packed end)
        // This handles maps with large intermediate sections (forge canvas maps)
        for (int postQuota = 0; postQuota <= 5000; postQuota++)
        {
            int candidate = packedEndBit - quotaTotalBits - postQuota;
            if (candidate <= forwardLimit) break; // already searched by forward pass
            if (candidate < objEndBit) break;

            int score = ScoreQuotaPosition(payload, candidate, qiCounts);
            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
            }
        }

        return bestCandidate >= 0 ? bestCandidate : objEndBit;
    }

    /// <summary>
    /// Scores a candidate quota position using structural validity checks.
    /// At the correct bit position, all quota entries satisfy min &lt;= max and placed &lt;= max.
    /// At wrong positions (even 1 bit off), bit misalignment causes some entries to have
    /// min &gt; max or placed &gt; max, which are heavily penalized.
    /// </summary>
    private int ScoreQuotaPosition(byte[] payload, int candidateBit,
        Dictionary<int, int> qiCounts)
    {
        int score = 0;

        foreach (var (qi, expectedCount) in qiCounts)
        {
            int entryBit = candidateBit + qi * 24;
            if (entryBit + 24 > payload.Length * 8) return int.MinValue;

            var (min, max, placed) = ReadQuotaEntryRaw(payload, entryBit);

            // Heavy penalty for structural violations (detects bit misalignment)
            if (min > max || (max > 0 && placed > max) || (max == 0 && placed > 0))
                score -= 100;
            // Reward for valid non-zero entries at used qi indices
            else if (placed > 0 && min <= max)
                score += expectedCount;
            // Mild penalty for zero entries at used qi indices
            else
                score -= 1;
        }

        return score;
    }

    /// <summary>
    /// Reads a quota entry (3 × 8-bit) directly from the byte array at an arbitrary bit offset.
    /// More efficient than creating a BitReader for each probe.
    /// </summary>
    private static (int min, int max, int placed) ReadQuotaEntryRaw(byte[] data, int bitOffset)
    {
        int bytePos = bitOffset / 8;
        int bitShift = bitOffset % 8;

        if (bytePos + 3 >= data.Length) return (0, 0, 0);

        if (bitShift == 0)
            return (data[bytePos], data[bytePos + 1], data[bytePos + 2]);

        int min = ((data[bytePos] << bitShift) | (data[bytePos + 1] >> (8 - bitShift))) & 0xFF;
        int max = ((data[bytePos + 1] << bitShift) | (data[bytePos + 2] >> (8 - bitShift))) & 0xFF;
        int placed = ((data[bytePos + 2] << bitShift) | (data[bytePos + 3] >> (8 - bitShift))) & 0xFF;

        return (min, max, placed);
    }

    /// <summary>
    /// Reads H4 quotas: 3 × 8-bit per entry (same as Reach).
    /// </summary>
    private void ReadQuotas(BitReader bits)
    {
        TagIndex = new List<TagIndexEntry>(MaxQuotas);

        int count = Math.Min(MaxQuotas, _numberOfQuotas);
        for (int i = 0; i < count; i++)
        {
            var entry = new TagIndexEntry();
            entry.Offset = i;
            entry.Ident = i;

            entry.RunTimeMinimum = (byte)bits.ReadInteger(8);
            entry.RunTimeMaximum = (byte)bits.ReadInteger(8);
            entry.CountOnMap = (byte)bits.ReadInteger(8);

            entry.Tag = new Tag
            {
                Ident = i,
                Path = $"Quota #{i}",
                Class = "h4_object",
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
    /// Resolves quota names from the Halo 4 palette database.
    /// Replaces synthetic "Quota #N" paths with real object names where available.
    /// </summary>
    private void ResolvePaletteNames()
    {
        var palette = Halo4PaletteDatabase.Create(MapId);
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
                entry.Tag.Class = palette.GetCategory(i) ?? "h4_object";
            }
            else if (entry.CountOnMap == 0 && entry.RunTimeMaximum == 0)
            {
                // Empty padding slot beyond the palette - hide from UI
                entry.Tag = null;
                entry.Ident = -1;
            }
        }
    }

    // ======== Interface methods (read-only stubs) ========

    public void SaveAll()
    {
        throw new NotSupportedException("Halo 4 MCC .mvar write support is not yet implemented.");
    }

    public void WriteHeader() { }
    public void WritePlacement(PlacementChunk chunk) { }
    public void WriteTagIndexEntry(TagIndexEntry entry) { }

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

    // ======== Helper methods ========

    private static int ReadIndexEncoded(BitReader bits, int valueBits)
    {
        bool absent = bits.ReadBool();
        if (absent) return -1;
        return (int)bits.ReadInteger(valueBits);
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
}
