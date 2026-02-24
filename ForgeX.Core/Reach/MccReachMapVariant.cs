using ForgeX.Core.IO;
using ForgeX.Core.Halo3;
using ForgeX.Core.Blf;

namespace ForgeX.Core.Reach;

/// <summary>
/// Reads Halo: Reach MCC .mvar files (BLF packed mvar, version 31).
/// Read-only: all write methods throw NotSupportedException.
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
    public bool CanWrite => false;
    public ReachPaletteDatabase? Palette { get; private set; }

    private int _numberOfQuotas;

    public MccReachMapVariant(BlfFile blfFile)
    {
        var mvar = blfFile.GetChunk("mvar")
            ?? throw new InvalidDataException("Missing mvar chunk.");

        byte[] payload = mvar.Data;

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
    /// Field order and widths differ significantly from Halo 3.
    /// </summary>
    private void ReadContentItemMetadata(BitReader bits)
    {
        // file_type: 4-bit unsigned, stored as value+1
        int fileType = (int)bits.ReadInteger(4) - 1;

        // size_in_bytes: 32-bit unsigned
        bits.ReadInteger(32);

        // unique_id, parent, root, game IDs: 64-bit each
        bits.ReadInteger64(64); // unique_id
        bits.ReadInteger64(64); // parent_unique_id
        bits.ReadInteger64(64); // root_unique_id
        bits.ReadInteger64(64); // game_id

        // activity: 3-bit unsigned, stored as value+1
        int activity = (int)bits.ReadInteger(3) - 1;

        // game_mode: 3-bit
        int gameMode = (int)bits.ReadInteger(3);

        // game_engine_type: 3-bit
        bits.ReadInteger(3);

        // map_id: 32-bit signed
        MapId = bits.ReadSignedInteger(32);

        // megalo_category_index: 8-bit signed
        bits.ReadSignedInteger(8);

        // creation_time: 64-bit
        bits.ReadInteger64(64);

        // creator_xuid: 64-bit
        bits.ReadInteger64(64);

        // creator_name: ASCII null-terminated, 16 bytes max
        MapAuthor = bits.ReadStringUtf8(16);

        // creator_xuid_is_online: 1 bit
        bits.ReadBool();

        // modification_time: 64-bit
        bits.ReadInteger64(64);

        // modifier_xuid: 64-bit
        bits.ReadInteger64(64);

        // modifier_name: ASCII null-terminated, 16 bytes max
        bits.ReadStringUtf8(16);

        // modifier_xuid_is_online: 1 bit
        bits.ReadBool();

        // name: wchar null-terminated, 128 wchars max
        VariantName = bits.ReadStringWchar(128);

        // description: wchar null-terminated, 128 wchars max
        VariantDescription = bits.ReadStringWchar(128);

        // Conditional fields based on file_type
        if (fileType == 3 || fileType == 4) // film
            bits.ReadSignedInteger(32); // seconds
        else if (fileType == 6) // game variant
            bits.ReadSignedInteger(8); // icon_index

        // Conditional based on activity
        if (activity == 2) // matchmaking
            bits.ReadInteger(16); // hopper_identifier

        // Conditional based on game_mode
        if (gameMode == 1) // campaign
        {
            bits.ReadInteger(8);  // campaign_id
            bits.ReadInteger(2);  // campaign_difficulty
            bits.ReadInteger(2);  // campaign_metagame_scoring
            bits.ReadInteger(8);  // campaign_insertion_point
            bits.ReadInteger(16); // campaign_primary_skulls
            bits.ReadInteger(16); // campaign_secondary_skulls
        }
        else if (gameMode == 2) // firefight
        {
            bits.ReadInteger(2);  // firefight_difficulty
            bits.ReadInteger(16); // firefight_primary_skulls
            bits.ReadInteger(16); // firefight_secondary_skulls
        }
    }

    /// <summary>
    /// Reads c_map_variant header (Reach TU1/MCC layout).
    /// Notable differences: scenario_palette_crc, built_from_xml, integer budgets.
    /// </summary>
    private void ReadMapVariantHeader(BitReader bits)
    {
        bits.ReadInteger(8);  // variant_version (31)
        bits.ReadInteger(32); // original_map_rsa_signature_hash
        bits.ReadInteger(32); // scenario_palette_crc

        _numberOfQuotas = (int)bits.ReadInteger(9);

        bits.ReadInteger(32); // map_id (second copy)
        bits.ReadBool();      // built_in
        bits.ReadBool();      // built_from_xml

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
    /// Template: max_strings=256, max_buffer=4096, offset_bits=12, buffer_size_bits=13, count_bits=9.
    /// Strings are read/skipped for now (not displayed in the UI).
    /// </summary>
    private void ReadStringTable(BitReader bits)
    {
        int stringCount = (int)bits.ReadInteger(9);

        // Per-string: 1-bit exists flag + optional 12-bit offset
        for (int i = 0; i < stringCount; i++)
        {
            bool exists = bits.ReadBool();
            if (exists)
                bits.ReadInteger(12); // byte offset into buffer
        }

        // Buffer data (only if strings exist)
        if (stringCount > 0)
        {
            int bufferSize = (int)bits.ReadInteger(13);
            bool isCompressed = bits.ReadBool();

            if (isCompressed)
            {
                int compressedSize = (int)bits.ReadInteger(13);
                bits.SkipBits(compressedSize * 8);
            }
            else
            {
                bits.SkipBits(bufferSize * 8);
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
            bits.ReadInteger(10); // skip, not used in UI

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
    /// Field order and widths differ significantly from Halo 3.
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
        bits.ReadInteger(8);

        // spawn_time: 8-bit
        placement.RespawnTime = (byte)bits.ReadInteger(8);

        // cached_type: 5-bit
        placement.ObjectType = (int)bits.ReadInteger(5);

        // label_index: index encoding (1-bit absent + 8-bit value)
        ReadIndexEncoded(bits, 8);

        // placement_flags: 8-bit
        placement.Flags = (byte)bits.ReadInteger(8);

        // team: 4-bit unsigned, stored as value+1 (-1 = neutral)
        int team = (int)bits.ReadInteger(4) - 1;
        placement.Team = (byte)Math.Max(0, team);

        // primary_change_color_index: index encoding (1-bit absent + 3-bit)
        ReadIndexEncoded(bits, 3);

        // Conditional fields based on cached_type
        switch (placement.ObjectType)
        {
            case 1: // weapon
                placement.SpareClips = (byte)bits.ReadInteger(8);
                break;
            case 12: // teleporter_receiver
            case 13: // teleporter_sender
            case 14: // teleporter_2way
                bits.ReadInteger(5); // channel
                bits.ReadInteger(5); // passability
                break;
            case 19: // location_name
                ReadIndexEncoded(bits, 8); // location_name_index
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
        }
    }

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

    public void WriteHeader() =>
        throw new NotSupportedException("Halo: Reach map variants are read-only.");

    public void WritePlacement(PlacementChunk chunk) =>
        throw new NotSupportedException("Halo: Reach map variants are read-only.");

    public void WriteTagIndexEntry(TagIndexEntry entry) =>
        throw new NotSupportedException("Halo: Reach map variants are read-only.");

    public void SaveAll() =>
        throw new NotSupportedException("Halo: Reach map variants are read-only.");

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
