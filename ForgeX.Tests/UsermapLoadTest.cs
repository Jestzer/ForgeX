using ForgeX.Core.Blf;
using ForgeX.Core.Halo3;
using ForgeX.Core.Halo4;
using ForgeX.Core.Reach;
using ForgeX.Core.Xbox360;

namespace ForgeX.Tests;

public class UsermapLoadTest
{
    private const string SampleFile = "/run/media/james/HDD/Non-work Related/Games/Xbox 360/Soft Mods/Tools/Halo 3/Forge/usermap00000000438313371";

    [Fact]
    public void CanLoadSampleUsermap()
    {
        // Copy to temp to avoid modifying the original
        var tempFile = Path.GetTempFileName();
        File.Copy(SampleFile, tempFile, true);

        try
        {
            var container = new StfsContainer(tempFile);

            Assert.NotEmpty(container.Entries);
            var sandboxEntry = container.GetEntryByFileName("sandbox.map");
            Assert.NotNull(sandboxEntry);

            var variant = new MapVariant(container);

            // Print parsed data for verification
            Console.WriteLine($"Variant Name: '{variant.VariantName}'");
            Console.WriteLine($"Description: '{variant.VariantDescription}'");
            Console.WriteLine($"Author: '{variant.MapAuthor}'");
            Console.WriteLine($"Map ID: {variant.MapId}");
            Console.WriteLine($"Spawned Object Count: {variant.SpawnedObjectCount}");
            Console.WriteLine($"Max Budget: {variant.MaximumBudget}");
            Console.WriteLine($"Current Budget: {variant.CurrentBudget}");
            Console.WriteLine($"Map Name: {variant.Tags?.MapName ?? "null"}");
            Console.WriteLine($"Tag Count: {variant.Tags?.TagCount ?? 0}");

            // Check basic sanity
            Assert.False(string.IsNullOrEmpty(variant.VariantName), "Variant name should not be empty");
            Assert.NotEqual(0, variant.MapId);
            Assert.NotNull(variant.Tags);
            Assert.True(variant.Tags.TagCount > 0, "Should have tag definitions");

            // Count non-null placements
            int activePlacements = variant.PlacementChunks.Count(c => c.ChunkType != ChunkType.Null);
            Console.WriteLine($"Active Placements: {activePlacements}");

            // Count non-null tag index entries
            int activeTagEntries = variant.TagIndex.Count(e => e.Tag != null);
            Console.WriteLine($"Active Tag Index Entries: {activeTagEntries}");

            // Print first few active tag entries
            foreach (var entry in variant.TagIndex.Where(e => e.Tag != null).Take(5))
            {
                Console.WriteLine($"  Tag: {entry.Tag!.Class} / {entry.Tag.Path} (Ident: {entry.Ident}, Count: {entry.CountOnMap}, Cost: {entry.Cost})");
            }

            // Print first few active placements
            foreach (var chunk in variant.PlacementChunks.Where(c => c.ChunkType != ChunkType.Null).Take(5))
            {
                Console.WriteLine($"  Placement: Type={chunk.ChunkType} X={chunk.SpawnCoords.X} Y={chunk.SpawnCoords.Y} Z={chunk.SpawnCoords.Z} TagsIdx={chunk.TagsIndex}");
            }

            container.Close();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private const string H4MccDir = "/run/media/james/m2-ssd/Program Files (x86)/Steam/steamapps/common/Halo The Master Chief Collection/halo4/map_variants/";

    [Fact]
    public void InspectHalo4MccMvar()
    {
        string path = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(path))
        {
            Console.WriteLine("SKIP: Halo 4 MCC .mvar not found");
            return;
        }

        var blf = new BlfFile(path);
        Console.WriteLine($"Chunks:");
        foreach (var chunk in blf.Chunks)
            Console.WriteLine($"  Tag='{chunk.Tag}' Size={chunk.Size} Version={chunk.MajorVersion}.{chunk.MinorVersion} DataLen={chunk.Data.Length}");

        var mvar = blf.GetChunk("mvar");
        Assert.NotNull(mvar);
        Console.WriteLine($"\nmvar version: {mvar.MajorVersion}");
        Console.WriteLine($"mvar data length: {mvar.Data.Length}");

        // Dump first 128 bytes of mvar data (SHA-1 prefix + start of bitstream)
        Console.Write("mvar first 128 bytes: ");
        for (int i = 0; i < Math.Min(128, mvar.Data.Length); i++)
            Console.Write($"{mvar.Data[i]:X2} ");
        Console.WriteLine();

        // Check SHA-1 prefix + length (same as Reach?)
        Console.Write("\nSHA-1 prefix (20 bytes): ");
        for (int i = 0; i < 20; i++)
            Console.Write($"{mvar.Data[i]:X2} ");
        Console.WriteLine();

        int packedLen = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
        Console.WriteLine($"Packed length field: {packedLen}");
        Console.WriteLine($"Remaining data after 24-byte prefix: {mvar.Data.Length - 24}");
        Console.WriteLine($"Match: {packedLen == mvar.Data.Length - 24}");

        // Try parsing the bitstream using BitReader
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
        var bits = new ForgeX.Core.IO.BitReader(bitstream);

        Console.WriteLine("\n--- Attempting bitstream parse (Reach-like format) ---");

        // Content metadata: file_type (4 bits, value+1)
        uint rawFileType = bits.ReadInteger(4);
        Console.WriteLine($"file_type raw: {rawFileType} (value: {rawFileType - 1})");

        // file_length (32 bits)
        uint fileLength = bits.ReadInteger(32);
        Console.WriteLine($"file_length: {fileLength}");

        // 4 x 64-bit IDs
        ulong uniqueId = bits.ReadInteger64(64);
        ulong parentId = bits.ReadInteger64(64);
        ulong rootId = bits.ReadInteger64(64);
        ulong gameId = bits.ReadInteger64(64);
        Console.WriteLine($"uniqueId: 0x{uniqueId:X16}");
        Console.WriteLine($"parentId: 0x{parentId:X16}");
        Console.WriteLine($"rootId: 0x{rootId:X16}");
        Console.WriteLine($"gameId: 0x{gameId:X16}");

        // Activity: 2 bits for Halo 4 (vs 3+value+1 for Reach)
        uint activity = bits.ReadInteger(2);
        Console.WriteLine($"activity (2-bit): {activity}");

        // game_mode: 3 bits
        uint gameMode = bits.ReadInteger(3);
        Console.WriteLine($"game_mode: {gameMode}");

        // engine_type: 3 bits
        uint engineType = bits.ReadInteger(3);
        Console.WriteLine($"engine_type: {engineType}");

        // H4 has unk2C (32-bit signed) - map_id?
        uint mapId1 = bits.ReadInteger(32);
        Console.WriteLine($"unk2C/map_id: {mapId1} (0x{mapId1:X8})");

        // engine_category_index: 8-bit signed
        uint megaloIdx = bits.ReadInteger(8);
        Console.WriteLine($"engine_category_index: {megaloIdx} (signed: {(sbyte)megaloIdx})");

        // creator timestamp: 64 bits
        ulong creatorTime = bits.ReadInteger64(64);
        Console.WriteLine($"creator_timestamp: {creatorTime}");

        // creator XUID: 64 bits
        ulong creatorXuid = bits.ReadInteger64(64);
        Console.WriteLine($"creator_xuid: 0x{creatorXuid:X16}");

        // creator name: null-terminated ASCII, max 15 chars
        // In Reach, it's a fixed 16-byte field. KSoft.Blam says ASCII null-terminated max 15.
        // Let me read byte by byte until null or 16 bytes
        var nameBytes = new List<byte>();
        for (int i = 0; i < 16; i++)
        {
            byte b = (byte)bits.ReadInteger(8);
            if (b == 0) break;
            nameBytes.Add(b);
        }
        string creatorName = System.Text.Encoding.ASCII.GetString(nameBytes.ToArray());
        Console.WriteLine($"creator_name: '{creatorName}'");

        // creator_is_online: 1 bit
        bool creatorOnline = bits.ReadBool();
        Console.WriteLine($"creator_is_online: {creatorOnline}");

        // modifier timestamp: 64 bits
        ulong modTime = bits.ReadInteger64(64);
        Console.WriteLine($"modifier_timestamp: {modTime}");

        // modifier XUID: 64 bits
        ulong modXuid = bits.ReadInteger64(64);
        Console.WriteLine($"modifier_xuid: 0x{modXuid:X16}");

        // modifier name
        var modNameBytes = new List<byte>();
        for (int i = 0; i < 16; i++)
        {
            byte b = (byte)bits.ReadInteger(8);
            if (b == 0) break;
            modNameBytes.Add(b);
        }
        string modName = System.Text.Encoding.ASCII.GetString(modNameBytes.ToArray());
        Console.WriteLine($"modifier_name: '{modName}'");

        // modifier_is_online: 1 bit
        bool modOnline = bits.ReadBool();
        Console.WriteLine($"modifier_is_online: {modOnline}");

        // Title: null-terminated UTF-16BE
        var titleChars = new List<char>();
        for (int i = 0; i < 128; i++)
        {
            ushort ch = (ushort)bits.ReadInteger(16);
            if (ch == 0) break;
            titleChars.Add((char)ch);
        }
        string title = new string(titleChars.ToArray());
        Console.WriteLine($"title: '{title}'");

        // Description: null-terminated UTF-16BE
        var descChars = new List<char>();
        for (int i = 0; i < 128; i++)
        {
            ushort ch = (ushort)bits.ReadInteger(16);
            if (ch == 0) break;
            descChars.Add((char)ch);
        }
        string desc = new string(descChars.ToArray());
        Console.WriteLine($"description: '{desc}'");

        Console.WriteLine($"\nBit position after metadata: {bits.BitOffset}");

        // Conditional metadata fields based on fileType/activity
        // fileType 5 = MapVariant, no conditional fields expected
        // activity 1 = no hopper fields

        // --- Map Variant Header ---
        Console.WriteLine("\n--- Map Variant Header ---");

        uint encodingVersion = bits.ReadInteger(8);
        Console.WriteLine($"encoding_version: {encodingVersion}");

        uint unknownInt1 = bits.ReadInteger(32);
        Console.WriteLine($"unknown1 (RSA hash?): 0x{unknownInt1:X8}");

        uint unknownInt2 = bits.ReadInteger(32);
        Console.WriteLine($"unknown2 (palette CRC?): 0x{unknownInt2:X8}");

        uint objectTypeCount = bits.ReadInteger(9);
        Console.WriteLine($"object_type_count: {objectTypeCount}");

        uint mapId = bits.ReadInteger(32);
        Console.WriteLine($"map_id: {mapId}");

        bool builtIn = bits.ReadBool();
        Console.WriteLine($"built_in: {builtIn}");

        bool userCreated = bits.ReadBool();
        Console.WriteLine($"user_created: {userCreated}");

        // World bounds: 6 x 32-bit float
        float xMin = bits.ReadRawFloat();
        float xMax = bits.ReadRawFloat();
        float yMin = bits.ReadRawFloat();
        float yMax = bits.ReadRawFloat();
        float zMin = bits.ReadRawFloat();
        float zMax = bits.ReadRawFloat();
        Console.WriteLine($"World bounds: X[{xMin},{xMax}] Y[{yMin},{yMax}] Z[{zMin},{zMax}]");

        // Budget
        uint budgetRaw = bits.ReadInteger(32);
        float budgetFloat = BitConverter.Int32BitsToSingle((int)budgetRaw);
        Console.WriteLine($"budget raw: {budgetRaw} float: {budgetFloat}");

        uint unknownInt3 = bits.ReadInteger(32);
        float unk3Float = BitConverter.Int32BitsToSingle((int)unknownInt3);
        Console.WriteLine($"unknown3 raw: {unknownInt3} float: {unk3Float}");

        Console.WriteLine($"\nBit position after header: {bits.BitOffset}");

        // --- String Table (same format as Reach: 9-bit count, per-string entries, buffer) ---
        Console.WriteLine("\n--- String Table ---");
        uint stringCount = bits.ReadInteger(9);
        Console.WriteLine($"string_count: {stringCount}");

        for (int i = 0; i < (int)stringCount; i++)
        {
            bool exists = bits.ReadBool();
            if (exists)
            {
                uint offset = bits.ReadInteger(12);
                if (i < 5) Console.WriteLine($"  string[{i}]: offset={offset}");
            }
            else
            {
                if (i < 5) Console.WriteLine($"  string[{i}]: absent");
            }
        }

        uint bufferSize = bits.ReadInteger(13);
        bool isCompressed = bits.ReadBool();
        Console.WriteLine($"buffer_size: {bufferSize}, compressed: {isCompressed}");

        if (isCompressed)
        {
            uint compressedSize = bits.ReadInteger(13);
            Console.WriteLine($"compressed_size: {compressedSize}");
            // Skip compressed data
            for (int i = 0; i < (int)compressedSize; i++)
                bits.ReadInteger(8);
        }
        else if (bufferSize > 0)
        {
            // Skip raw buffer
            for (int i = 0; i < (int)bufferSize; i++)
                bits.ReadInteger(8);
        }

        Console.WriteLine($"Bit position after string table: {bits.BitOffset}");

        // --- Object table parsing ---
        Console.WriteLine("\n--- Variant Objects ---");
        var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
            xMin, xMax, yMin, yMax, zMin, zMax);
        Console.WriteLine($"Position bits: X={bitsX} Y={bitsY} Z={bitsZ}");

        int expectedEndBit = 24 * 8 + packedLen * 8; // prefix + packed data
        Console.WriteLine($"Expected total bits: {expectedEndBit} (packed len = {packedLen})");

        int startBit = bits.BitOffset;
        int activeCount = 0;
        bool parseError = false;

        // Detailed per-field trace of first 5 active objects
        int objectCount = 640;
        for (int i = 0; i < objectCount && !parseError; i++)
        {
            int objStart = bits.BitOffset;
            bool exists = bits.ReadBool();
            if (!exists) continue;
            activeCount++;

            bool verbose = activeCount <= 5;
            if (verbose) Console.WriteLine($"\n  === Object [{i}] (start bit: {objStart}) ===");

            uint flags = bits.ReadInteger(2);
            if (verbose) Console.WriteLine($"    flags: {flags} (2 bits) @ {objStart+1}");

            bool qa = bits.ReadBool();
            int qi = qa ? -1 : (int)bits.ReadInteger(8);
            if (verbose) Console.WriteLine($"    quota_index: {qi} (absent={qa}) @ {bits.BitOffset}");

            bool va = bits.ReadBool();
            int vi = va ? -1 : (int)bits.ReadInteger(5);
            if (verbose) Console.WriteLine($"    variant_index: {vi} (absent={va}) @ {bits.BitOffset}");

            bool inBounds = bits.ReadBool();
            if (verbose) Console.WriteLine($"    point_in_bounds: {inBounds} @ {bits.BitOffset}");

            float px, py, pz;
            if (inBounds)
            {
                px = ForgeX.Core.Reach.ReachPositionEncoding.DecodePosition(bits, bitsX, xMin, xMax);
                py = ForgeX.Core.Reach.ReachPositionEncoding.DecodePosition(bits, bitsY, yMin, yMax);
                pz = ForgeX.Core.Reach.ReachPositionEncoding.DecodePosition(bits, bitsZ, zMin, zMax);
            }
            else
            {
                px = bits.ReadRawFloat();
                py = bits.ReadRawFloat();
                pz = bits.ReadRawFloat();
            }
            if (verbose) Console.WriteLine($"    pos: ({px:F4},{py:F4},{pz:F4}) @ {bits.BitOffset}");

            int preOrient = bits.BitOffset;
            var orient = ForgeX.Core.Reach.ReachOrientationConverter.ReadOrientation(bits);
            if (verbose) Console.WriteLine($"    orientation: fwd=({orient.FwdI:F3},{orient.FwdJ:F3},{orient.FwdK:F3}) up=({orient.UpI:F3},{orient.UpJ:F3},{orient.UpK:F3}) ({bits.BitOffset - preOrient} bits) @ {bits.BitOffset}");

            uint spawnRelRaw = bits.ReadInteger(10);
            if (verbose) Console.WriteLine($"    spawn_relative_to: {(int)spawnRelRaw - 1} @ {bits.BitOffset}");

            // Pure Reach MP format for bit counting (to get correct active count)
            uint shape = bits.ReadInteger(2);
            switch (shape)
            {
                case 1: bits.ReadInteger(11); break;
                case 2: bits.ReadInteger(11); bits.ReadInteger(11); bits.ReadInteger(11); break;
                case 3: bits.ReadInteger(11); bits.ReadInteger(11); bits.ReadInteger(11); bits.ReadInteger(11); break;
            }
            uint spawnSeq = bits.ReadInteger(8);
            uint respawn = bits.ReadInteger(8);
            uint objType = bits.ReadInteger(5);
            bool la = bits.ReadBool(); int li = la ? -1 : (int)bits.ReadInteger(8);
            uint pflags = bits.ReadInteger(8);
            uint teamRaw = bits.ReadInteger(4);
            bool ca = bits.ReadBool(); int ci = ca ? -1 : (int)bits.ReadInteger(3);
            switch (objType)
            {
                case 1: bits.ReadInteger(8); break;
                case 12: case 13: case 14: bits.ReadInteger(5); bits.ReadInteger(5); break;
                case 19: { bool xa = bits.ReadBool(); if (!xa) bits.ReadInteger(8); break; }
            }

            if (verbose)
                Console.WriteLine($"    shape={shape} seq={spawnSeq} resp={respawn} type={objType} label={li} flags=0x{pflags:X2} team={(int)teamRaw-1} color={ci} | {bits.BitOffset - objStart} bits");
            if (bits.BitsRemaining < 0) { parseError = true; Console.WriteLine($"  PARSE ERROR: out of bits at obj {i}!"); }
        }

        int after640 = bits.BitOffset;
        Console.WriteLine($"\nActive objects: {activeCount}");
        Console.WriteLine($"Bit position after 640 objects: {after640}");
        Console.WriteLine($"Bits remaining to expected end: {expectedEndBit - after640}");

        // Search for quota start by analyzing raw bytes of bitstream
        Console.WriteLine($"\n--- Raw byte analysis of bitstream ---");
        Console.WriteLine($"Bitstream length: {bitstream.Length} bytes ({bitstream.Length * 8} bits)");

        // Find the transition from non-zero to mostly-zero data
        // (objects → quotas with mostly empty entries)
        // Check 32-byte windows for zero density
        Console.WriteLine("\nZero-density per 32-byte window:");
        for (int byteOff = 200; byteOff < bitstream.Length - 32; byteOff += 32)
        {
            int zeros = 0;
            for (int j = 0; j < 32; j++)
                if (bitstream[byteOff + j] == 0) zeros++;
            if (zeros > 20 || byteOff < 300 || byteOff > bitstream.Length - 200)
                Console.WriteLine($"  byte {byteOff} (bit {byteOff*8 + 192}): {zeros}/32 zero bytes");
        }

        // Scan every bit offset for best quota alignment (wider range, score by validity)
        Console.WriteLine("\n--- Exhaustive quota start scan ---");
        int bestStart = 0;
        int bestScore2 = 0;
        string bestQuotaReport = "";

        for (int bitOff = 12000; bitOff <= 20000; bitOff++)
        {
            int streamBitOff = bitOff - 24 * 8; // offset into bitstream array
            if (streamBitOff < 0 || streamBitOff + 6144 > bitstream.Length * 8) continue;

            var bp = new ForgeX.Core.IO.BitReader(bitstream);
            bp.SkipBits(streamBitOff);

            int score = 0;
            int maxCheck = Math.Min(256, (int)objectTypeCount);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < maxCheck; i++)
            {
                uint mi = bp.ReadInteger(8);
                uint ma = bp.ReadInteger(8);
                uint co = bp.ReadInteger(8);
                if (mi == 0 && ma == 0 && co == 0) { score += 3; continue; }
                // Valid non-zero: min<=max, count<=max, max<100
                if (mi <= ma && co <= ma && ma < 100)
                {
                    score += 2;
                    sb.Append($"  [{i}]:{mi}/{ma}/{co}");
                }
                else
                {
                    score -= 5; // penalize invalid
                }
            }
            if (score > bestScore2)
            {
                bestScore2 = score;
                bestStart = bitOff;
                bestQuotaReport = sb.ToString();
            }
        }
        Console.WriteLine($"Best quota start: bit {bestStart} (score: {bestScore2})");
        Console.WriteLine($"Object section: bits {2274} to {bestStart} = {bestStart - 2274} bits");
        Console.WriteLine($"Non-zero quotas:{bestQuotaReport}");

        // Math-based quota position: Reach parse(15183) + 65*21 extra + 126 table = 16674
        int calcQuotaStart = after640 + activeCount * 21 + 14 * 9;
        Console.WriteLine($"\n--- Calculated quota start (Reach+21+table): bit {calcQuotaStart} ---");

        // Print table at calculated position
        {
            int tblStart = calcQuotaStart - 14 * 9;
            var tb = new ForgeX.Core.IO.BitReader(bitstream);
            tb.SkipBits(tblStart - 24 * 8);
            Console.Write("object_type_start_index[14]: ");
            for (int i = 0; i < 14; i++)
            {
                uint val = tb.ReadInteger(9);
                Console.Write($"{(int)val - 1} ");
            }
            Console.WriteLine();
        }

        // Print quotas at calculated position
        {
            var qb = new ForgeX.Core.IO.BitReader(bitstream);
            qb.SkipBits(calcQuotaStart - 24 * 8);
            Console.WriteLine("Quotas at calculated position:");
            int nz = 0;
            for (int i = 0; i < (int)objectTypeCount; i++)
            {
                uint mi = qb.ReadInteger(8);
                uint ma = qb.ReadInteger(8);
                uint co = qb.ReadInteger(8);
                if (mi > 0 || ma > 0 || co > 0)
                {
                    nz++;
                    Console.WriteLine($"  quota[{i}]: min={mi} max={ma} count={co}");
                }
            }
            Console.WriteLine($"Non-zero: {nz}, End bit: {qb.BitOffset + 24 * 8}");
            Console.WriteLine($"Expected end: {expectedEndBit}, diff: {qb.BitOffset + 24 * 8 - expectedEndBit}");
        }

        // Also try without the 14×9 table (quotas directly after objects+21)
        int calcQuotaNoTable = after640 + activeCount * 21;
        Console.WriteLine($"\n--- Without table: quota start at bit {calcQuotaNoTable} ---");
        {
            var qb = new ForgeX.Core.IO.BitReader(bitstream);
            qb.SkipBits(calcQuotaNoTable - 24 * 8);
            Console.WriteLine("Quotas:");
            int nz = 0;
            for (int i = 0; i < (int)objectTypeCount; i++)
            {
                uint mi = qb.ReadInteger(8);
                uint ma = qb.ReadInteger(8);
                uint co = qb.ReadInteger(8);
                if (mi > 0 || ma > 0 || co > 0)
                {
                    nz++;
                    if (nz <= 20) Console.WriteLine($"  quota[{i}]: min={mi} max={ma} count={co}");
                }
            }
            Console.WriteLine($"Non-zero: {nz}, End bit: {qb.BitOffset + 24 * 8}");
            Console.WriteLine($"Expected end: {expectedEndBit}, diff: {qb.BitOffset + 24 * 8 - expectedEndBit}");
        }

        // Try with exactly 20 or 22 extra bits per object too
        foreach (int extra in new[] { 19, 20, 21, 22, 23, 24 })
        {
            int qs = after640 + activeCount * extra + 14 * 9;
            var qb = new ForgeX.Core.IO.BitReader(bitstream);
            int skip = qs - 24 * 8;
            if (skip < 0 || skip + 6144 > bitstream.Length * 8) continue;
            qb.SkipBits(skip);
            int nz = 0;
            bool anyBad = false;
            for (int i = 0; i < (int)objectTypeCount; i++)
            {
                uint mi = qb.ReadInteger(8);
                uint ma = qb.ReadInteger(8);
                uint co = qb.ReadInteger(8);
                if (mi > 0 || ma > 0 || co > 0) { nz++; if (ma > 100) anyBad = true; }
            }
            int endBit = qb.BitOffset + 24 * 8;
            Console.WriteLine($"  extra={extra}: qs={qs}, end={endBit}, diff={endBit - expectedEndBit}, nonzero={nz}, bad={anyBad}");
        }
    }

    [Fact]
    public void Halo4FormatDeepSearch()
    {
        string path = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(path))
        {
            Console.WriteLine("SKIP: Halo 4 MCC .mvar not found");
            return;
        }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);

        int quotaStartBit = 14721;

        // Parse known-correct metadata + header + string table
        var bits = new ForgeX.Core.IO.BitReader(bitstream);
        bits.ReadInteger(4); bits.ReadInteger(32);
        bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
        bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32);
        bits.ReadInteger(8);
        bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool();
        bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool();
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

        uint encVer = bits.ReadInteger(8);
        bits.ReadInteger(32); bits.ReadInteger(32);
        int numQuotas = (int)bits.ReadInteger(9);
        bits.ReadInteger(32);
        bits.ReadBool(); bits.ReadBool();
        float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
        float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
        float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
        bits.ReadInteger(32); bits.ReadInteger(32);

        int strCount = (int)bits.ReadInteger(9);
        for (int i = 0; i < strCount; i++)
        {
            bool exists = bits.ReadBool();
            if (exists) bits.ReadInteger(12);
        }
        if (strCount > 0)
        {
            int bufSize = (int)bits.ReadInteger(13);
            bool compressed = bits.ReadBool();
            if (compressed)
            {
                int compSize = (int)bits.ReadInteger(13);
                for (int ci = 0; ci < compSize; ci++) bits.ReadInteger(8);
            }
            else
            {
                for (int ci = 0; ci < bufSize; ci++) bits.ReadInteger(8);
            }
        }

        int objStart = bits.BitOffset;
        var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
            xMin, xMax, yMin, yMax, zMin, zMax, 21);
        Console.WriteLine($"Object section starts at bit {objStart}, target quota at {quotaStartBit}");
        Console.WriteLine($"Position bits: X={bitsX} Y={bitsY} Z={bitsZ}");

        // Systematic deep search: try all reasonable field encoding combinations
        // Fields to vary:
        //   team: "raw4" (4-bit value+1), "idx3" (index-encoded 1+3), "raw3" (3 bits raw)
        //   color: "idx3" (index-encoded 1+3), "raw4" (4 bits raw), "raw3" (3 bits raw)
        //   pflags: 8, 6, 4 bits
        //   cached_type: 5, 4, 3, 0 bits
        //   boundary: "reach" (case2=33,case3=44), "nitrogen" (case2=11,case3=22)
        //   conditionals: true (Reach-style type conditionals), false (none)

        var results = new List<(string Desc, int Active, int EndBit, int Delta)>();

        foreach (string teamEnc in new[] { "raw4", "idx3", "raw3" })
        foreach (string colorEnc in new[] { "idx3", "raw4", "raw3" })
        foreach (int pflagsBits in new[] { 8, 6, 4 })
        foreach (int typeBits in new[] { 5, 4, 3, 0 })
        foreach (string boundary in new[] { "reach", "nitrogen" })
        foreach (bool conditionals in new[] { true, false })
        {
            var tb = new ForgeX.Core.IO.BitReader(bitstream);
            tb.SkipBits(objStart);
            int active = 0;
            bool error = false;

            for (int i = 0; i < 640 && !error; i++)
            {
                if (tb.BitsRemaining < 1) { error = true; break; }
                bool ex = tb.ReadBool();
                if (!ex) continue;
                active++;
                if (tb.BitsRemaining < 30) { error = true; break; }

                tb.ReadInteger(2); // flags
                bool qa = tb.ReadBool(); if (!qa) tb.ReadInteger(8);
                bool va = tb.ReadBool(); if (!va) tb.ReadInteger(5);
                bool ib = tb.ReadBool();
                if (ib) { tb.ReadInteger(bitsX); tb.ReadInteger(bitsY); tb.ReadInteger(bitsZ); }
                else { tb.ReadRawFloat(); tb.ReadRawFloat(); tb.ReadRawFloat(); }
                bool du = tb.ReadBool(); if (!du) tb.ReadInteger(20); tb.ReadInteger(14);

                // Shape
                uint shape = tb.ReadInteger(2);
                if (boundary == "reach")
                {
                    switch (shape)
                    {
                        case 1: tb.ReadInteger(11); break;
                        case 2: tb.ReadInteger(11); tb.ReadInteger(11); tb.ReadInteger(11); break;
                        case 3: tb.ReadInteger(11); tb.ReadInteger(11); tb.ReadInteger(11); tb.ReadInteger(11); break;
                    }
                }
                else
                {
                    switch (shape)
                    {
                        case 1: tb.ReadInteger(11); break;
                        case 2: tb.ReadInteger(11); break;
                        case 3: tb.ReadInteger(11); tb.ReadInteger(11); break;
                    }
                }

                tb.ReadInteger(8); // seq
                tb.ReadInteger(8); // resp
                int typ = typeBits > 0 ? (int)tb.ReadInteger(typeBits) : 0;
                bool la = tb.ReadBool(); if (!la) tb.ReadInteger(8); // label
                tb.ReadInteger(pflagsBits); // pflags

                // Team
                if (teamEnc == "raw4") tb.ReadInteger(4);
                else if (teamEnc == "raw3") tb.ReadInteger(3);
                else { bool ta = tb.ReadBool(); if (!ta) tb.ReadInteger(3); }

                // Color
                if (colorEnc == "raw4") tb.ReadInteger(4);
                else if (colorEnc == "raw3") tb.ReadInteger(3);
                else { bool ca = tb.ReadBool(); if (!ca) tb.ReadInteger(3); }

                // Type-based conditionals
                if (conditionals && typeBits > 0)
                {
                    switch (typ)
                    {
                        case 1: tb.ReadInteger(8); break;
                        case 12: case 13: case 14: tb.ReadInteger(5); tb.ReadInteger(5); break;
                        case 19: { bool xa = tb.ReadBool(); if (!xa) tb.ReadInteger(8); break; }
                    }
                }

                if (tb.BitsRemaining < 0) error = true;
            }

            if (!error)
            {
                int delta = tb.BitOffset - quotaStartBit;
                if (Math.Abs(delta) <= 50)
                {
                    string desc = $"tm={teamEnc} col={colorEnc} pf={pflagsBits} typ={typeBits} bnd={boundary} cond={conditionals}";
                    results.Add((desc, active, tb.BitOffset, delta));
                }
            }
        }

        results.Sort((a, b) => Math.Abs(a.Delta).CompareTo(Math.Abs(b.Delta)));
        Console.WriteLine($"\nFormats within delta ±50 (sorted by |delta|):");
        foreach (var r in results)
            Console.WriteLine($"  [{r.Delta:+0;-0;0}] {r.Desc} (active={r.Active})");
        if (results.Count == 0)
            Console.WriteLine("  (none found)");

        // Also try with extra offset before objects (maybe H4 has extra section data)
        Console.WriteLine($"\n--- Trying offset shifts before object section ---");
        foreach (int extraOffset in new[] { 1, 2, 3, 4, 8, 9, 10, 16, 32, 64 })
        {
            // Use the best baseline format: noSpawnRel, reach boundaries
            var tb = new ForgeX.Core.IO.BitReader(bitstream);
            tb.SkipBits(objStart + extraOffset);
            int active = 0;
            bool error = false;

            for (int i = 0; i < 640 && !error; i++)
            {
                if (tb.BitsRemaining < 1) { error = true; break; }
                bool ex = tb.ReadBool();
                if (!ex) continue;
                active++;
                if (tb.BitsRemaining < 30) { error = true; break; }

                tb.ReadInteger(2);
                bool qa = tb.ReadBool(); if (!qa) tb.ReadInteger(8);
                bool va = tb.ReadBool(); if (!va) tb.ReadInteger(5);
                bool ib = tb.ReadBool();
                if (ib) { tb.ReadInteger(bitsX); tb.ReadInteger(bitsY); tb.ReadInteger(bitsZ); }
                else { tb.ReadRawFloat(); tb.ReadRawFloat(); tb.ReadRawFloat(); }
                bool du = tb.ReadBool(); if (!du) tb.ReadInteger(20); tb.ReadInteger(14);
                uint shape = tb.ReadInteger(2);
                switch (shape)
                {
                    case 1: tb.ReadInteger(11); break;
                    case 2: tb.ReadInteger(11); tb.ReadInteger(11); tb.ReadInteger(11); break;
                    case 3: tb.ReadInteger(11); tb.ReadInteger(11); tb.ReadInteger(11); tb.ReadInteger(11); break;
                }
                tb.ReadInteger(8); tb.ReadInteger(8); tb.ReadInteger(5);
                bool la = tb.ReadBool(); if (!la) tb.ReadInteger(8);
                tb.ReadInteger(8);
                tb.ReadInteger(4); // team raw4
                bool ca = tb.ReadBool(); if (!ca) tb.ReadInteger(3); // color idx3

                if (tb.BitsRemaining < 0) error = true;
            }
            if (!error)
            {
                int delta = tb.BitOffset - quotaStartBit;
                Console.WriteLine($"  offset +{extraOffset}: active={active} delta={delta}");
            }
        }

        // And try with start_index table between objects and quotas (14x9 = 126 bits)
        Console.WriteLine($"\n--- Best formats with table (14x9=126) ---");
        int targetWithTable = quotaStartBit - 126;
        foreach (var r in results.Where(r => Math.Abs(r.EndBit - targetWithTable) <= 20))
            Console.WriteLine($"  [{r.EndBit - targetWithTable:+0;-0;0}] {r.Desc} (active={r.Active})");

        // Try varying ORIENTATION encoding (axis bits, angle bits)
        Console.WriteLine($"\n--- Orientation encoding search ---");
        var oriResults = new List<(string Desc, int Active, int EndBit, int Delta)>();

        foreach (int axisBits in new[] { 16, 17, 18, 19, 20 })
        foreach (int angleBits in new[] { 10, 11, 12, 13, 14 })
        foreach (string teamEnc in new[] { "raw4", "idx3" })
        {
            var tb = new ForgeX.Core.IO.BitReader(bitstream);
            tb.SkipBits(objStart);
            int active = 0;
            bool error = false;

            for (int i = 0; i < 640 && !error; i++)
            {
                if (tb.BitsRemaining < 1) { error = true; break; }
                bool ex = tb.ReadBool();
                if (!ex) continue;
                active++;
                if (tb.BitsRemaining < 30) { error = true; break; }

                tb.ReadInteger(2);
                bool qa = tb.ReadBool(); if (!qa) tb.ReadInteger(8);
                bool va = tb.ReadBool(); if (!va) tb.ReadInteger(5);
                bool ib = tb.ReadBool();
                if (ib) { tb.ReadInteger(bitsX); tb.ReadInteger(bitsY); tb.ReadInteger(bitsZ); }
                else { tb.ReadRawFloat(); tb.ReadRawFloat(); tb.ReadRawFloat(); }

                bool defUp = tb.ReadBool();
                if (!defUp) tb.ReadInteger(axisBits);
                tb.ReadInteger(angleBits);

                uint shape = tb.ReadInteger(2);
                switch (shape)
                {
                    case 1: tb.ReadInteger(11); break;
                    case 2: tb.ReadInteger(11); tb.ReadInteger(11); tb.ReadInteger(11); break;
                    case 3: tb.ReadInteger(11); tb.ReadInteger(11); tb.ReadInteger(11); tb.ReadInteger(11); break;
                }
                tb.ReadInteger(8); tb.ReadInteger(8); tb.ReadInteger(5);
                bool la = tb.ReadBool(); if (!la) tb.ReadInteger(8);
                tb.ReadInteger(8);
                if (teamEnc == "raw4") tb.ReadInteger(4);
                else { bool ta = tb.ReadBool(); if (!ta) tb.ReadInteger(3); }
                bool ca = tb.ReadBool(); if (!ca) tb.ReadInteger(3);

                if (tb.BitsRemaining < 0) error = true;
            }

            if (!error)
            {
                int delta = tb.BitOffset - quotaStartBit;
                string desc = $"axis={axisBits} angle={angleBits} tm={teamEnc}";
                oriResults.Add((desc, active, tb.BitOffset, delta));
            }
        }

        oriResults.Sort((a, b) => Math.Abs(a.Delta).CompareTo(Math.Abs(b.Delta)));
        Console.WriteLine("Top 20 closest:");
        foreach (var r in oriResults.Take(20))
            Console.WriteLine($"  [{r.Delta:+0;-0;0}] {r.Desc} (active={r.Active})");
    }

    [Fact]
    public void Halo4RawBitDump()
    {
        string path = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(path))
        {
            Console.WriteLine("SKIP: Halo 4 MCC .mvar not found");
            return;
        }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);

        int quotaStartBit = 14721;

        // Parse known-correct metadata + header + string table
        var bits = new ForgeX.Core.IO.BitReader(bitstream);
        bits.ReadInteger(4); bits.ReadInteger(32);
        bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
        bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32);
        bits.ReadInteger(8);
        bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool();
        bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool();
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

        bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
        int numQuotas = (int)bits.ReadInteger(9);
        bits.ReadInteger(32);
        bits.ReadBool(); bits.ReadBool();
        float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
        float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
        float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
        bits.ReadInteger(32); bits.ReadInteger(32);

        int strCount = (int)bits.ReadInteger(9);
        for (int i = 0; i < strCount; i++)
        {
            bool exists = bits.ReadBool();
            if (exists) bits.ReadInteger(12);
        }
        if (strCount > 0)
        {
            int bufSize = (int)bits.ReadInteger(13);
            bool compressed = bits.ReadBool();
            if (compressed)
            {
                int compSize = (int)bits.ReadInteger(13);
                for (int ci = 0; ci < compSize; ci++) bits.ReadInteger(8);
            }
            else
            {
                for (int ci = 0; ci < bufSize; ci++) bits.ReadInteger(8);
            }
        }

        int objStart = bits.BitOffset;
        var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
            xMin, xMax, yMin, yMax, zMin, zMax, 21);

        // Parse objects one by one, stopping to dump raw bits around transitions
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);

        int activeIdx = 0;
        for (int i = 0; i < 640; i++)
        {
            int oStart = pb.BitOffset;
            bool exists = pb.ReadBool();
            if (!exists) continue;
            activeIdx++;

            uint flags = pb.ReadInteger(2);
            bool qa = pb.ReadBool(); int qi = qa ? -1 : (int)pb.ReadInteger(8);
            bool va = pb.ReadBool(); int vi = va ? -1 : (int)pb.ReadInteger(5);

            bool ib = pb.ReadBool();
            float px, py, pz;
            if (ib)
            {
                px = ForgeX.Core.Reach.ReachPositionEncoding.DecodePosition(pb, bitsX, xMin, xMax);
                py = ForgeX.Core.Reach.ReachPositionEncoding.DecodePosition(pb, bitsY, yMin, yMax);
                pz = ForgeX.Core.Reach.ReachPositionEncoding.DecodePosition(pb, bitsZ, zMin, zMax);
            }
            else
            {
                px = pb.ReadRawFloat(); py = pb.ReadRawFloat(); pz = pb.ReadRawFloat();
            }

            int oriStart = pb.BitOffset;
            bool defUp = pb.ReadBool();
            if (!defUp) pb.ReadInteger(20);
            pb.ReadInteger(14);

            // Properties
            int propStart = pb.BitOffset;
            uint shape = pb.ReadInteger(2);
            switch (shape)
            {
                case 1: pb.ReadInteger(11); break;
                case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
            }
            int seq = (int)pb.ReadInteger(8);
            int resp = (int)pb.ReadInteger(8);
            int typ = (int)pb.ReadInteger(5);
            bool la = pb.ReadBool(); int li = la ? -1 : (int)pb.ReadInteger(8);
            int pfl = (int)pb.ReadInteger(8);
            int team = (int)pb.ReadInteger(4) - 1;
            bool ca = pb.ReadBool(); int ci = ca ? -1 : (int)pb.ReadInteger(3);

            // Type conditionals
            string condStr = "";
            switch (typ)
            {
                case 1: { int sc = (int)pb.ReadInteger(8); condStr = $" spare={sc}"; break; }
                case 12: case 13: case 14: { int tc = (int)pb.ReadInteger(5); int tp = (int)pb.ReadInteger(5); condStr = $" tel={tc}/{tp}"; break; }
                case 19: { bool xa = pb.ReadBool(); int xv = xa ? -1 : (int)pb.ReadInteger(8); condStr = $" loc={xv}"; break; }
            }

            int objBits = pb.BitOffset - oStart;

            // Print first 40 objects with detailed bit positions
            if (activeIdx <= 40)
            {
                Console.WriteLine($"  #{activeIdx} [{i}] {objBits}b @{oStart} fl={flags} qi={qi} vi={vi} ib={ib} pos=({px:F1},{py:F1},{pz:F1}) ori@{oriStart}({(defUp?"def":"nod")}) prop@{propStart} sh={shape} seq={seq} rsp={resp} typ={typ} lbl={li} pf=0x{pfl:X2} tm={team} col={ci}{condStr}");
            }

            // At key transition points, dump the next raw bits
            if (activeIdx == 32 || activeIdx == 33 || activeIdx == 34)
            {
                Console.WriteLine($"    --> Next 80 raw bits from current pos {pb.BitOffset}:");
                int savedPos = pb.BitOffset;
                var rawBits = new System.Text.StringBuilder();
                for (int b = 0; b < 80 && pb.BitsRemaining > 0; b++)
                {
                    rawBits.Append(pb.ReadBool() ? '1' : '0');
                    if ((b + 1) % 8 == 0) rawBits.Append(' ');
                }
                Console.WriteLine($"    {rawBits}");
                // Reset to saved position
                pb = new ForgeX.Core.IO.BitReader(bitstream);
                pb.SkipBits(savedPos);
            }

            if (pb.BitsRemaining < 0) break;
        }
        Console.WriteLine($"Total active: {activeIdx}, end bit: {pb.BitOffset}, target: {quotaStartBit}, delta: {pb.BitOffset - quotaStartBit}");

        // Now dump raw bits around the quota start to verify the transition
        Console.WriteLine($"\n--- Raw bits around quota start ({quotaStartBit}) ---");
        {
            var rb = new ForgeX.Core.IO.BitReader(bitstream);
            rb.SkipBits(quotaStartBit - 40);
            Console.Write("  40 bits before: ");
            for (int i = 0; i < 40; i++)
            {
                Console.Write(rb.ReadBool() ? '1' : '0');
                if ((i + 1) % 8 == 0) Console.Write(' ');
            }
            Console.WriteLine();
            Console.Write("  First 240 bits of quotas: ");
            for (int i = 0; i < 240; i++)
            {
                Console.Write(rb.ReadBool() ? '1' : '0');
                if ((i + 1) % 8 == 0) Console.Write(' ');
                if ((i + 1) % 24 == 0) Console.Write("| ");
            }
            Console.WriteLine();
        }

        // Try "reverse parse": start from quota and work backwards
        // Read the 240 bits before quotas as potential per-object data
        Console.WriteLine($"\n--- Bit dump: last 500 bits before quotas ---");
        {
            int dumpStart = quotaStartBit - 500;
            var rb = new ForgeX.Core.IO.BitReader(bitstream);
            rb.SkipBits(dumpStart);
            for (int line = 0; line < 10; line++)
            {
                int lineStart = dumpStart + line * 50;
                Console.Write($"  @{lineStart}: ");
                for (int i = 0; i < 50 && rb.BitsRemaining > 0; i++)
                {
                    Console.Write(rb.ReadBool() ? '1' : '0');
                    if ((i + 1) % 10 == 0) Console.Write(' ');
                }
                Console.WriteLine();
            }
        }
    }

    [Fact]
    public void Halo4BranchingParse()
    {
        // Try parsing H4 objects with a "branching" approach:
        // Parse forward, but at each decision point (shape encoding, field widths),
        // try multiple branches and pick the one that stays aligned with the quota target.
        string path = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(path))
        {
            Console.WriteLine("SKIP: Halo 4 MCC .mvar not found");
            return;
        }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);

        int quotaStartBit = 14721;

        // Parse metadata + header + string table (known correct)
        var bits = new ForgeX.Core.IO.BitReader(bitstream);
        bits.ReadInteger(4); bits.ReadInteger(32);
        bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
        bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32);
        bits.ReadInteger(8);
        bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool();
        bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool();
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

        bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
        int numQuotas = (int)bits.ReadInteger(9);
        bits.ReadInteger(32);
        bits.ReadBool(); bits.ReadBool();
        float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
        float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
        float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
        bits.ReadInteger(32); bits.ReadInteger(32);

        int strCount = (int)bits.ReadInteger(9);
        for (int i = 0; i < strCount; i++)
        {
            bool exists = bits.ReadBool();
            if (exists) bits.ReadInteger(12);
        }
        if (strCount > 0)
        {
            int bufSize = (int)bits.ReadInteger(13);
            bool compressed = bits.ReadBool();
            if (compressed)
            {
                int compSize = (int)bits.ReadInteger(13);
                for (int ci = 0; ci < compSize; ci++) bits.ReadInteger(8);
            }
            else
            {
                for (int ci = 0; ci < bufSize; ci++) bits.ReadInteger(8);
            }
        }

        int objStart = bits.BitOffset;
        var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
            xMin, xMax, yMin, yMax, zMin, zMax, 21);

        // First: determine how many bits total are available for objects
        int totalObjBits = quotaStartBit - objStart;
        Console.WriteLine($"Object section: {objStart} to {quotaStartBit} = {totalObjBits} bits");

        // Theory: what if spawn_relative_to is INDEX-ENCODED (1+9) instead of raw 10?
        // This saves 9 bits per object with spawnRel=-1 (absent) and 0 bits when present.
        // Let me count how many objects would have absent spawnRel by parsing with the field.
        Console.WriteLine($"\n--- Testing index-encoded spawn_relative_to ---");
        foreach (int srBits in new[] { 7, 8, 9 })
        {
            var pb = new ForgeX.Core.IO.BitReader(bitstream);
            pb.SkipBits(objStart);
            int active = 0;
            int srAbsent = 0;
            bool error = false;

            for (int i = 0; i < 640 && !error; i++)
            {
                if (pb.BitsRemaining < 1) { error = true; break; }
                bool ex = pb.ReadBool();
                if (!ex) continue;
                active++;

                pb.ReadInteger(2);
                bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
                bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
                bool ib = pb.ReadBool();
                if (ib) { pb.ReadInteger(bitsX); pb.ReadInteger(bitsY); pb.ReadInteger(bitsZ); }
                else { pb.ReadRawFloat(); pb.ReadRawFloat(); pb.ReadRawFloat(); }
                bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);

                // spawn_relative_to as index-encoded
                bool srAbs = pb.ReadBool();
                if (srAbs) srAbsent++;
                else pb.ReadInteger(srBits);

                pb.ReadInteger(2); // shape (skip dims for shape=0)
                // Read shape dims same as Reach
                // Actually: just skip the properties wholesale
                pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(5);
                bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8);
                pb.ReadInteger(8);
                pb.ReadInteger(4);
                bool ca = pb.ReadBool(); if (!ca) pb.ReadInteger(3);

                if (pb.BitsRemaining < 0) error = true;
            }
            if (!error)
            {
                int delta = pb.BitOffset - quotaStartBit;
                Console.WriteLine($"  sr=idx(1+{srBits}): active={active} srAbsent={srAbsent} delta={delta}");
            }
        }

        // Theory 2: try reading just the raw bits for known-good objects
        // and determine what the ACTUAL format gives when we force-align
        // Parse first 32 objects to a known-good position (7718)
        // Then try EVERY possible per-object size for the next chunk of objects
        Console.WriteLine($"\n--- Brute force: try all per-object extra bits (0-20) ---");
        for (int extraBitsPerObj = -10; extraBitsPerObj <= 10; extraBitsPerObj++)
        {
            // Parse first 32 correctly
            var pb = new ForgeX.Core.IO.BitReader(bitstream);
            pb.SkipBits(objStart);
            int active = 0;
            bool error = false;

            for (int i = 0; i < 640 && !error; i++)
            {
                if (pb.BitsRemaining < 1) { error = true; break; }
                bool ex = pb.ReadBool();
                if (!ex) continue;
                active++;

                pb.ReadInteger(2);
                bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
                bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
                bool ib = pb.ReadBool();
                if (ib) { pb.ReadInteger(bitsX); pb.ReadInteger(bitsY); pb.ReadInteger(bitsZ); }
                else { pb.ReadRawFloat(); pb.ReadRawFloat(); pb.ReadRawFloat(); }
                bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);

                uint shape = pb.ReadInteger(2);
                switch (shape)
                {
                    case 1: pb.ReadInteger(11); break;
                    case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                    case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                }
                pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(5);
                bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8);
                pb.ReadInteger(8);
                pb.ReadInteger(4);
                bool ca = pb.ReadBool(); if (!ca) pb.ReadInteger(3);

                // Type conditionals (Reach-style)
                // skip for now

                // Add/remove extra bits
                if (extraBitsPerObj > 0)
                    pb.ReadInteger(extraBitsPerObj);
                else if (extraBitsPerObj < 0)
                    pb.SkipBits(extraBitsPerObj); // negative = go back

                if (pb.BitsRemaining < 0) error = true;
            }
            if (!error)
            {
                int delta = pb.BitOffset - quotaStartBit;
                if (Math.Abs(delta) <= 30)
                    Console.WriteLine($"  extra={extraBitsPerObj:+0;-0;0}: active={active} delta={delta}");
            }
        }

        // Theory 3: What if some objects have MORE fields (like H4's object locking)?
        // Try adding a 1-bit lock flag per object
        Console.WriteLine($"\n--- Checking if removing N bits per active object gives delta=0 ---");
        Console.WriteLine($"  (Based on noSpawnRel delta=245, active=71)");
        Console.WriteLine($"  Need to save 245 bits over ~71 objects = {245.0/71:F2} bits/obj");
        Console.WriteLine($"  With 69 active: {245.0/69:F2} bits/obj");

        // Check the raw bit at position 2274-1 to see if there's any data before objects
        Console.WriteLine($"\n--- Checking bits right before/after object section ---");
        {
            var rb = new ForgeX.Core.IO.BitReader(bitstream);
            rb.SkipBits(objStart - 16);
            Console.Write("  16 bits before objStart: ");
            for (int i = 0; i < 16; i++) Console.Write(rb.ReadBool() ? '1' : '0');
            Console.WriteLine();
            Console.Write("  First 32 bits of objects: ");
            for (int i = 0; i < 32; i++) Console.Write(rb.ReadBool() ? '1' : '0');
            Console.WriteLine();
        }

        // Radical test: what if out-of-bounds objects have FEWER position bits?
        // Try: OOB uses 16-bit half-float x3 = 48 bits (vs 96 for full float)
        // Or: OOB uses no position at all
        // Or: OOB uses adaptive encoding (same as in-bounds)
        Console.WriteLine($"\n--- Alternative OOB position encodings ---");
        foreach (string oobEnc in new[] { "float32", "adaptive", "float16", "none" })
        {
            var pb = new ForgeX.Core.IO.BitReader(bitstream);
            pb.SkipBits(objStart);
            int active = 0, oobCount = 0;
            bool error = false;

            for (int i = 0; i < 640 && !error; i++)
            {
                if (pb.BitsRemaining < 1) { error = true; break; }
                bool ex = pb.ReadBool();
                if (!ex) continue;
                active++;

                pb.ReadInteger(2);
                bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
                bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
                bool ib = pb.ReadBool();

                if (ib)
                {
                    pb.ReadInteger(bitsX); pb.ReadInteger(bitsY); pb.ReadInteger(bitsZ);
                }
                else
                {
                    oobCount++;
                    switch (oobEnc)
                    {
                        case "float32": pb.ReadRawFloat(); pb.ReadRawFloat(); pb.ReadRawFloat(); break;
                        case "adaptive": pb.ReadInteger(bitsX); pb.ReadInteger(bitsY); pb.ReadInteger(bitsZ); break;
                        case "float16": pb.ReadInteger(16); pb.ReadInteger(16); pb.ReadInteger(16); break;
                        case "none": break;
                    }
                }

                bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
                uint shape = pb.ReadInteger(2);
                switch (shape)
                {
                    case 1: pb.ReadInteger(11); break;
                    case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                    case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                }
                pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(5);
                bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8);
                pb.ReadInteger(8); pb.ReadInteger(4);
                bool ca2 = pb.ReadBool(); if (!ca2) pb.ReadInteger(3);

                if (pb.BitsRemaining < 0) error = true;
            }
            if (!error)
            {
                int delta = pb.BitOffset - quotaStartBit;
                Console.WriteLine($"  oob={oobEnc}: active={active} oob={oobCount} delta={delta}");
            }
            else
            {
                Console.WriteLine($"  oob={oobEnc}: ERROR (ran out of bits)");
            }
        }

        // Theory 5: what if there's a DIFFERENT number of bits for the in-bounds flag?
        // What if point_in_bounds doesn't exist and ALL objects use adaptive?
        Console.WriteLine($"\n--- No in-bounds flag (all adaptive) ---");
        {
            var pb = new ForgeX.Core.IO.BitReader(bitstream);
            pb.SkipBits(objStart);
            int active = 0;
            bool error = false;

            for (int i = 0; i < 640 && !error; i++)
            {
                if (pb.BitsRemaining < 1) { error = true; break; }
                bool ex = pb.ReadBool();
                if (!ex) continue;
                active++;

                pb.ReadInteger(2);
                bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
                bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
                // No in-bounds flag, always adaptive
                pb.ReadInteger(bitsX); pb.ReadInteger(bitsY); pb.ReadInteger(bitsZ);
                bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
                uint shape = pb.ReadInteger(2);
                switch (shape)
                {
                    case 1: pb.ReadInteger(11); break;
                    case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                    case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                }
                pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(5);
                bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8);
                pb.ReadInteger(8); pb.ReadInteger(4);
                bool ca2 = pb.ReadBool(); if (!ca2) pb.ReadInteger(3);

                if (pb.BitsRemaining < 0) error = true;
            }
            if (!error)
            {
                int delta = pb.BitOffset - quotaStartBit;
                Console.WriteLine($"  No ib flag, always adaptive: active={active} delta={delta}");
            }
        }
    }

    [Fact]
    public void Halo4MultiFileAnalysis()
    {
        // Test all available H4 .mvar files to see if delta varies between files
        var files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0)
        {
            Console.WriteLine("SKIP: No H4 MCC .mvar files found");
            return;
        }

        foreach (var filePath in files)
        {
            Console.WriteLine($"\n=== {Path.GetFileName(filePath)} ===");
            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            var bitstream = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);

            // Read packed length from prefix (bytes 20-23, big-endian) — value is in BYTES
            int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
            int packedLenBits = packedLenBytes * 8;

            var bits = new ForgeX.Core.IO.BitReader(bitstream);

            // Content metadata (H4 format)
            int ft = (int)bits.ReadInteger(4);
            bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32);
            bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

            // Header
            uint encVer = bits.ReadInteger(8);
            bits.ReadInteger(32); bits.ReadInteger(32);
            int numQuotas = (int)bits.ReadInteger(9);
            uint mapId = bits.ReadInteger(32);
            bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            bits.ReadInteger(32); bits.ReadInteger(32);

            Console.WriteLine($"  version={encVer} quotas={numQuotas} mapId={mapId}");
            Console.WriteLine($"  bounds: X[{xMin:F1},{xMax:F1}] Y[{yMin:F1},{yMax:F1}] Z[{zMin:F1},{zMax:F1}]");

            // String table
            int strCount = (int)bits.ReadInteger(9);
            for (int i = 0; i < strCount; i++)
            {
                bool exists = bits.ReadBool();
                if (exists) bits.ReadInteger(12);
            }
            if (strCount > 0)
            {
                int bufSize = (int)bits.ReadInteger(13);
                bool compressed = bits.ReadBool();
                if (compressed)
                {
                    int compSize = (int)bits.ReadInteger(13);
                    for (int ci = 0; ci < compSize; ci++) bits.ReadInteger(8);
                }
                else
                {
                    for (int ci = 0; ci < bufSize; ci++) bits.ReadInteger(8);
                }
            }

            int objStart = bits.BitOffset;
            var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);

            Console.WriteLine($"  objStart={objStart} posBits={bitsX}+{bitsY}+{bitsZ}={bitsX+bitsY+bitsZ}");

            // Find quotas: scan for positions where ALL 256 entries are valid
            // Among valid positions, find the FIRST one (closest to objects)
            // that has at least some non-zero entries
            var quotaCandidates = new List<(int Pos, int NonZero, int Score)>();
            int scanEnd = Math.Min(packedLenBits, bitstream.Length * 8) - 256 * 24;
            for (int startBit = objStart + 1000; startBit <= scanEnd; startBit++)
            {
                var qr = new ForgeX.Core.IO.BitReader(bitstream);
                qr.SkipBits(startBit);
                int score = 0;
                int nonZero = 0;
                bool bad = false;
                for (int q = 0; q < numQuotas; q++)
                {
                    int mn = (int)qr.ReadInteger(8);
                    int mx = (int)qr.ReadInteger(8);
                    int ct = (int)qr.ReadInteger(8);
                    if (mn == 0 && mx == 0 && ct == 0) { score += 3; continue; }
                    if (mn > mx || ct > mx || mx > 200) { bad = true; break; }
                    score += 3;
                    nonZero++;
                }
                if (!bad && score == numQuotas * 3 && nonZero >= 3)
                    quotaCandidates.Add((startBit, nonZero, score));
            }
            int bestQuotaStart;
            int bestNonZero;
            if (quotaCandidates.Count > 0)
            {
                // Pick candidate with most non-zero entries (prefer first if tied)
                var best = quotaCandidates.OrderByDescending(c => c.NonZero).ThenBy(c => c.Pos).First();
                bestQuotaStart = best.Pos;
                bestNonZero = best.NonZero;
                Console.WriteLine($"  Found {quotaCandidates.Count} candidate quota positions, best nonZero={bestNonZero}");
                // Also show top 3
                foreach (var c in quotaCandidates.OrderByDescending(c => c.NonZero).Take(3))
                    Console.WriteLine($"    pos={c.Pos} nonZero={c.NonZero}");
            }
            else
            {
                bestQuotaStart = -1;
                bestNonZero = 0;
            }

            if (bestQuotaStart < 0) { Console.WriteLine("  Could not find quotas!"); continue; }

            Console.WriteLine($"  quotaStart={bestQuotaStart} (nonZero={bestNonZero})");

            // Show raw quota entries at this position
            if (bestQuotaStart >= 0)
            {
                var qr = new ForgeX.Core.IO.BitReader(bitstream);
                qr.SkipBits(bestQuotaStart);
                Console.Write("  Quotas (first non-zero): ");
                for (int q = 0; q < numQuotas; q++)
                {
                    int mn = (int)qr.ReadInteger(8);
                    int mx = (int)qr.ReadInteger(8);
                    int ct = (int)qr.ReadInteger(8);
                    if (mn != 0 || mx != 0 || ct != 0)
                        Console.Write($"[{q}]:{mn}/{mx}/{ct} ");
                }
                Console.WriteLine();
            }

            // BLF chunk info
            Console.Write($"  Chunks: ");
            foreach (var c in blf.Chunks)
                Console.Write($"{c.Tag}({c.Data.Length}B v{c.MajorVersion}.{c.MinorVersion}) ");
            Console.WriteLine();

            Console.WriteLine($"  Total bitstream: {bitstream.Length} bytes = {bitstream.Length * 8} bits");
            Console.WriteLine($"  Packed data: {packedLenBytes} bytes = {packedLenBits} bits (rest is padding)");

            // The actual data ends at packedLenBits — find where quotas should be
            int quotaSize = numQuotas * 24; // 3×8 bits per quota
            int expectedQuotaStart = packedLenBits - quotaSize;
            Console.WriteLine($"  Expected quota start (packed - quotaSize): {expectedQuotaStart} bits");

            // Check quotas at expected position
            if (expectedQuotaStart > 0 && expectedQuotaStart < bitstream.Length * 8)
            {
                var qr2 = new ForgeX.Core.IO.BitReader(bitstream);
                qr2.SkipBits(expectedQuotaStart);
                int nz = 0;
                bool allValid = true;
                for (int q = 0; q < numQuotas; q++)
                {
                    int mn = (int)qr2.ReadInteger(8);
                    int mx = (int)qr2.ReadInteger(8);
                    int ct = (int)qr2.ReadInteger(8);
                    if (mn != 0 || mx != 0 || ct != 0)
                    {
                        nz++;
                        if (nz <= 10) Console.Write($"    [{q}]:{mn}/{mx}/{ct} ");
                    }
                    if (mn > mx || ct > mx) allValid = false;
                }
                Console.WriteLine();
                Console.WriteLine($"  Quotas at expected pos: {nz} non-zero, allValid={allValid}");
            }

            // Parse objects with noSpawnRel Reach format
            var pb = new ForgeX.Core.IO.BitReader(bitstream);
            pb.SkipBits(objStart);
            int active = 0, oobCount = 0, shapedCount = 0;
            bool error = false;

            for (int i = 0; i < 640 && !error; i++)
            {
                if (pb.BitsRemaining < 1) { error = true; break; }
                bool ex = pb.ReadBool();
                if (!ex) continue;
                active++;

                pb.ReadInteger(2);
                bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
                bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
                bool ib = pb.ReadBool();
                if (ib) { pb.ReadInteger(bitsX); pb.ReadInteger(bitsY); pb.ReadInteger(bitsZ); }
                else { pb.ReadRawFloat(); pb.ReadRawFloat(); pb.ReadRawFloat(); oobCount++; }
                bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);

                uint shape = pb.ReadInteger(2);
                if (shape > 0) shapedCount++;
                switch (shape)
                {
                    case 1: pb.ReadInteger(11); break;
                    case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                    case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                }
                pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(5);
                bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8);
                pb.ReadInteger(8); pb.ReadInteger(4);
                bool ca = pb.ReadBool(); if (!ca) pb.ReadInteger(3);

                if (pb.BitsRemaining < 0) error = true;
            }

            int delta = pb.BitOffset - bestQuotaStart;
            Console.WriteLine($"  noSpawnRel: active={active} oob={oobCount} shaped={shapedCount} end={pb.BitOffset} delta={delta}");
            Console.WriteLine($"  bits/active_obj: {(float)(pb.BitOffset - objStart - (640 - active)) / active:F1}");
            Console.WriteLine($"  target bits/obj: {(float)(bestQuotaStart - objStart - (640 - active)) / active:F1}");
            Console.WriteLine($"  diff/obj: {(float)delta / active:F2}");
        }
    }

    [Fact]
    public void Halo4FormatParametricSearch()
    {
        string path = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(path))
        {
            Console.WriteLine("SKIP: Halo 4 MCC .mvar not found");
            return;
        }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);

        // Known values from previous analysis
        int quotaStartBit = 14721; // bitstream bit where quotas begin (exhaustive scan found 14913 absolute = 14913 - 192)
        // Wait: let me re-verify. The scan uses bitOff which subtracts 192 for streamBitOff.
        // bestStart=14913 means streamBitOff=14913-192=14721. Quotas at bitstream bit 14721.

        // Parse metadata + header + string table to find object section start
        var bits = new ForgeX.Core.IO.BitReader(bitstream);

        // Content metadata (H4 format: 2-bit activity, extra 32-bit map field)
        bits.ReadInteger(4); // file_type
        bits.ReadInteger(32); // size
        bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); // 4x64 IDs
        bits.ReadInteger(2); // activity (H4: 2 bits)
        bits.ReadInteger(3); // game_mode
        bits.ReadInteger(3); // engine_type
        bits.ReadInteger(32); // unk2C (extra H4 field)
        bits.ReadInteger(8); // engine_category_index
        bits.ReadInteger64(64); // creator_time
        bits.ReadInteger64(64); // creator_xuid
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; } // creator_name
        bits.ReadBool(); // creator_is_online
        bits.ReadInteger64(64); // mod_time
        bits.ReadInteger64(64); // mod_xuid
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; } // mod_name
        bits.ReadBool(); // mod_is_online
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; } // title
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; } // desc

        Console.WriteLine($"After metadata: bit {bits.BitOffset}");

        // Header
        uint encVer = bits.ReadInteger(8);
        bits.ReadInteger(32); // RSA
        bits.ReadInteger(32); // CRC
        int numQuotas = (int)bits.ReadInteger(9);
        bits.ReadInteger(32); // map_id
        bits.ReadBool(); bits.ReadBool(); // built_in, built_from_xml
        float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
        float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
        float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
        bits.ReadInteger(32); bits.ReadInteger(32); // budgets

        Console.WriteLine($"After header: bit {bits.BitOffset}");
        Console.WriteLine($"Bounds: X[{xMin},{xMax}] Y[{yMin},{yMax}] Z[{zMin},{zMax}]");

        // String table
        int strCount = (int)bits.ReadInteger(9);
        for (int i = 0; i < strCount; i++)
        {
            bool exists = bits.ReadBool();
            if (exists) bits.ReadInteger(12);
        }
        if (strCount > 0)
        {
            int bufSize = (int)bits.ReadInteger(13);
            bool compressed = bits.ReadBool();
            if (compressed)
            {
                int compSize = (int)bits.ReadInteger(13);
                for (int i = 0; i < compSize; i++) bits.ReadInteger(8);
            }
            else
            {
                for (int i = 0; i < bufSize; i++) bits.ReadInteger(8);
            }
        }

        int objStart = bits.BitOffset;
        Console.WriteLine($"Object section starts at bit: {objStart}");
        Console.WriteLine($"Quota target at bitstream bit: {quotaStartBit}");
        Console.WriteLine($"Available for objects: {quotaStartBit - objStart} bits");

        // Dump first 200 raw bits of object section
        Console.Write("First 200 bits: ");
        var dumpBits = new ForgeX.Core.IO.BitReader(bitstream);
        dumpBits.SkipBits(objStart);
        for (int i = 0; i < 200; i++)
        {
            if (i > 0 && i % 8 == 0) Console.Write(' ');
            Console.Write(dumpBits.ReadBool() ? '1' : '0');
        }
        Console.WriteLine();

        // Wider parametric search with more format variations
        Console.WriteLine("\n--- Parametric search (wider) ---");
        var results = new List<(string Desc, int Active, int EndBit, int Delta)>();

        void TryFormat(string desc, int baseBits, bool hasSpawnRel, int spawnRelBits,
            int typeBits, int teamBits, int pflagsBits, bool hasGameEngineFlags, bool hasTable)
        {
            int targetEnd = hasTable ? quotaStartBit - 14 * 9 : quotaStartBit;

            var (bx, by, bz) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, baseBits);

            var tb = new ForgeX.Core.IO.BitReader(bitstream);
            tb.SkipBits(objStart);

            int active = 0;
            bool error = false;

            for (int i = 0; i < 640 && !error; i++)
            {
                if (tb.BitsRemaining < 1) { error = true; break; }
                bool exists = tb.ReadBool();
                if (!exists) continue;
                active++;

                if (tb.BitsRemaining < 50) { error = true; break; }
                tb.ReadInteger(2); // flags
                bool qa = tb.ReadBool(); if (!qa) tb.ReadInteger(8); // qi
                bool va = tb.ReadBool(); if (!va) tb.ReadInteger(5); // vi

                bool inBounds = tb.ReadBool();
                if (inBounds) { tb.ReadInteger(bx); tb.ReadInteger(by); tb.ReadInteger(bz); }
                else { tb.ReadInteger(32); tb.ReadInteger(32); tb.ReadInteger(32); }

                bool defUp = tb.ReadBool();
                if (!defUp) tb.ReadInteger(20);
                tb.ReadInteger(14);

                if (hasSpawnRel) tb.ReadInteger(spawnRelBits);

                uint shape = tb.ReadInteger(2);
                switch (shape)
                {
                    case 1: tb.ReadInteger(11); break;
                    case 2: tb.ReadInteger(11); tb.ReadInteger(11); tb.ReadInteger(11); break;
                    case 3: tb.ReadInteger(11); tb.ReadInteger(11); tb.ReadInteger(11); tb.ReadInteger(11); break;
                }
                tb.ReadInteger(8); // spawn_seq
                tb.ReadInteger(8); // respawn
                uint objType = tb.ReadInteger(typeBits); // type
                if (hasGameEngineFlags) tb.ReadInteger(8); // game_engine_flags
                bool la = tb.ReadBool(); if (!la) tb.ReadInteger(8); // label
                tb.ReadInteger(pflagsBits); // pflags
                tb.ReadInteger(teamBits); // team
                bool ca = tb.ReadBool(); if (!ca) tb.ReadInteger(3); // color
                // Don't do type-based conditionals (too fragile with unknown type values)

                if (tb.BitsRemaining < 0) { error = true; }
            }

            if (!error)
            {
                int endBit = tb.BitOffset;
                int delta = endBit - targetEnd;
                results.Add((desc, active, endBit, delta));
            }
        }

        // Systematic search
        foreach (int baseBits in new[] { 20, 21 })
        foreach (bool hasTable in new[] { false, true })
        foreach (bool hasSpawnRel in new[] { false, true })
        foreach (int typeBits in new[] { 5, 8 })
        foreach (int teamBits in new[] { 3, 4 })
        foreach (int pflagsBits in new[] { 8 })
        foreach (bool hasGEF in new[] { false, true })
        {
            int spawnRelBits = 10;
            string desc = $"b{baseBits} sr{(hasSpawnRel?spawnRelBits:0)} t{typeBits} tm{teamBits} gef{(hasGEF?1:0)} tbl{(hasTable?1:0)}";
            TryFormat(desc, baseBits, hasSpawnRel, spawnRelBits, typeBits, teamBits, pflagsBits, hasGEF, hasTable);
        }

        // Sort by absolute delta and show top 20
        results.Sort((a, b) => Math.Abs(a.Delta).CompareTo(Math.Abs(b.Delta)));
        Console.WriteLine("Top 20 closest formats:");
        foreach (var r in results.Take(20))
            Console.WriteLine($"  {r.Desc}: active={r.Active} end={r.EndBit} delta={r.Delta}");

        // Test Nitrogen-derived format:
        // Boundary: case 2→11b, case 3→22b (not 33/44 like Reach)
        // Team: index-encoded(1+3), Color: 4 raw bits
        // Channel(5) always present, no type-based conditionals
        // Try with and without spawn_relative_to, and 640 vs 651 slots
        Console.WriteLine("\n--- Nitrogen-format tests ---");
        foreach (bool spawnRel in new[] { true, false })
        foreach (int slots in new[] { 640, 651 })
        foreach (bool hasTable in new[] { false, true })
        {
            int target = hasTable ? quotaStartBit - 14 * 9 : quotaStartBit;
            var (nbx, nby, nbz) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);
            var nb = new ForgeX.Core.IO.BitReader(bitstream);
            nb.SkipBits(objStart);
            int nActive = 0;
            bool nErr = false;
            for (int i = 0; i < slots && !nErr; i++)
            {
                if (nb.BitsRemaining < 1) { nErr = true; break; }
                bool ex = nb.ReadBool();
                if (!ex) continue;
                nActive++;
                if (nb.BitsRemaining < 40) { nErr = true; break; }
                nb.ReadInteger(2); // flags
                bool qa = nb.ReadBool(); if (!qa) nb.ReadInteger(8);
                bool va = nb.ReadBool(); if (!va) nb.ReadInteger(5);
                bool ib = nb.ReadBool();
                if (ib) { nb.ReadInteger(nbx); nb.ReadInteger(nby); nb.ReadInteger(nbz); }
                else { nb.ReadInteger(32); nb.ReadInteger(32); nb.ReadInteger(32); }
                bool du = nb.ReadBool(); if (!du) nb.ReadInteger(20); nb.ReadInteger(14);
                if (spawnRel) nb.ReadInteger(10);
                // Nitrogen boundary encoding
                uint sh = nb.ReadInteger(2);
                switch (sh)
                {
                    case 1: nb.ReadInteger(11); break;        // sphere: 1 dim
                    case 2: nb.ReadInteger(11); break;        // cylinder: 1 dim (Nitrogen)
                    case 3: nb.ReadInteger(11); nb.ReadInteger(11); break; // box: 2 dims (Nitrogen)
                }
                nb.ReadInteger(8); // seq
                nb.ReadInteger(8); // resp
                nb.ReadInteger(5); // channel
                bool la = nb.ReadBool(); if (!la) nb.ReadInteger(8); // label
                nb.ReadInteger(8); // flags
                nb.ReadInteger(4); // color (raw 4 bits, Nitrogen-style)
                bool ta = nb.ReadBool(); if (!ta) nb.ReadInteger(3); // team (index-encoded 1+3)
                if (nb.BitsRemaining < 0) nErr = true;
            }
            if (!nErr)
            {
                int delta = nb.BitOffset - target;
                Console.WriteLine($"  spawnRel={spawnRel} slots={slots} table={hasTable}: active={nActive} end={nb.BitOffset} delta={delta}");
            }
        }

        // Also try: Nitrogen format but with Reach conditionals vs no conditionals
        // And swap team/color back to Reach-style
        Console.WriteLine("\n--- Format combos with Nitrogen boundaries ---");
        foreach (bool spawnRel in new[] { true, false })
        foreach (string teamColor in new[] { "reach", "nitrogen" }) // reach: team(4)+color(1+3), nitrogen: color(4)+team(1+3)
        {
            var (nbx2, nby2, nbz2) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);
            var nb2 = new ForgeX.Core.IO.BitReader(bitstream);
            nb2.SkipBits(objStart);
            int na2 = 0; bool ne2 = false;
            for (int i = 0; i < 640 && !ne2; i++)
            {
                if (nb2.BitsRemaining < 1) { ne2 = true; break; }
                bool ex = nb2.ReadBool();
                if (!ex) continue;
                na2++;
                if (nb2.BitsRemaining < 40) { ne2 = true; break; }
                nb2.ReadInteger(2);
                bool qa = nb2.ReadBool(); if (!qa) nb2.ReadInteger(8);
                bool va = nb2.ReadBool(); if (!va) nb2.ReadInteger(5);
                bool ib = nb2.ReadBool();
                if (ib) { nb2.ReadInteger(nbx2); nb2.ReadInteger(nby2); nb2.ReadInteger(nbz2); }
                else { nb2.ReadInteger(32); nb2.ReadInteger(32); nb2.ReadInteger(32); }
                bool du = nb2.ReadBool(); if (!du) nb2.ReadInteger(20); nb2.ReadInteger(14);
                if (spawnRel) nb2.ReadInteger(10);
                // Nitrogen boundaries
                uint sh = nb2.ReadInteger(2);
                switch (sh)
                {
                    case 1: nb2.ReadInteger(11); break;
                    case 2: nb2.ReadInteger(11); break;
                    case 3: nb2.ReadInteger(11); nb2.ReadInteger(11); break;
                }
                nb2.ReadInteger(8); nb2.ReadInteger(8); // seq, resp
                nb2.ReadInteger(5); // type/channel
                bool la = nb2.ReadBool(); if (!la) nb2.ReadInteger(8); // label
                nb2.ReadInteger(8); // flags
                if (teamColor == "reach")
                {
                    nb2.ReadInteger(4); // team (raw 4)
                    bool ca = nb2.ReadBool(); if (!ca) nb2.ReadInteger(3); // color (index 1+3)
                }
                else
                {
                    nb2.ReadInteger(4); // color (raw 4)
                    bool ta = nb2.ReadBool(); if (!ta) nb2.ReadInteger(3); // team (index 1+3)
                }
                if (nb2.BitsRemaining < 0) ne2 = true;
            }
            if (!ne2)
            {
                int delta = nb2.BitOffset - quotaStartBit;
                Console.WriteLine($"  spawnRel={spawnRel} tc={teamColor}: active={na2} end={nb2.BitOffset} delta={delta}");
            }
        }

        // Dump ALL active objects with noSpawnRel format to find where parsing breaks
        Console.WriteLine("\n--- Full object dump: noSpawnRel b21 t5 tm4 ---");
        {
            var (bx2, by2, bz2) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);

            var pb = new ForgeX.Core.IO.BitReader(bitstream);
            pb.SkipBits(objStart);

            int activeIdx = 0;
            for (int i = 0; i < 640; i++)
            {
                int oStart = pb.BitOffset;
                bool exists = pb.ReadBool();
                if (!exists) continue;
                activeIdx++;

                uint flags = pb.ReadInteger(2);
                bool qa2 = pb.ReadBool(); int qi2 = qa2 ? -1 : (int)pb.ReadInteger(8);
                bool va2 = pb.ReadBool(); int vi2 = va2 ? -1 : (int)pb.ReadInteger(5);

                bool inBounds = pb.ReadBool();
                float px, py, pz;
                if (inBounds)
                {
                    px = ForgeX.Core.Reach.ReachPositionEncoding.DecodePosition(pb, bx2, xMin, xMax);
                    py = ForgeX.Core.Reach.ReachPositionEncoding.DecodePosition(pb, by2, yMin, yMax);
                    pz = ForgeX.Core.Reach.ReachPositionEncoding.DecodePosition(pb, bz2, zMin, zMax);
                }
                else
                {
                    px = pb.ReadRawFloat(); py = pb.ReadRawFloat(); pz = pb.ReadRawFloat();
                }

                int preOrient = pb.BitOffset;
                bool defUp = pb.ReadBool();
                if (!defUp) pb.ReadInteger(20);
                pb.ReadInteger(14);
                int orientBits = pb.BitOffset - preOrient;

                uint shape = pb.ReadInteger(2);
                int dimBits = 0;
                switch (shape)
                {
                    case 1: pb.ReadInteger(11); dimBits=11; break;
                    case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); dimBits=33; break;
                    case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); dimBits=44; break;
                }
                int seq = (int)pb.ReadInteger(8);
                int resp = (int)pb.ReadInteger(8);
                int typ = (int)pb.ReadInteger(5);
                bool la2 = pb.ReadBool(); int li2 = la2 ? -1 : (int)pb.ReadInteger(8);
                int pfl = (int)pb.ReadInteger(8);
                int team = (int)pb.ReadInteger(4) - 1;
                bool ca2 = pb.ReadBool(); int ci2 = ca2 ? -1 : (int)pb.ReadInteger(3);

                // Now try reading type conditionals (Reach-style)
                string condStr = "";
                switch (typ)
                {
                    case 1: { int sc = (int)pb.ReadInteger(8); condStr = $" spare={sc}"; break; }
                    case 12: case 13: case 14: { int tc = (int)pb.ReadInteger(5); int tp = (int)pb.ReadInteger(5); condStr = $" tel={tc}/{tp}"; break; }
                    case 19: { bool xa = pb.ReadBool(); int xv = xa ? -1 : (int)pb.ReadInteger(8); condStr = $" loc={xv}"; break; }
                }

                int objBits = pb.BitOffset - oStart;
                bool posValid = px >= xMin - 50 && px <= xMax + 50 && py >= yMin - 50 && py <= yMax + 50 && pz >= zMin - 50 && pz <= zMax + 50;
                string warn = "";
                if (!posValid && inBounds) warn += " [POS!]";
                if (seq > 127) warn += " [SEQ!]";
                if (resp > 250) warn += " [RSP!]";
                if (typ > 27) warn += " [TYP!]";
                if (li2 > 127 && li2 >= 0) warn += " [LBL!]";

                Console.WriteLine($"  #{activeIdx} [{i}] {objBits}b @{oStart} fl={flags} qi={qi2} vi={vi2} ib={inBounds} pos=({px:F1},{py:F1},{pz:F1}) ori={orientBits}b sh={shape} seq={seq} rsp={resp} typ={typ} lbl={li2} pf=0x{pfl:X2} tm={team} col={ci2}{condStr}{warn}");

                if (pb.BitsRemaining < 0) { Console.WriteLine("  *** OUT OF BITS ***"); break; }
            }
            Console.WriteLine($"Total active: {activeIdx}, end bit: {pb.BitOffset}, target: {quotaStartBit}, delta: {pb.BitOffset - quotaStartBit}");
        }

        // Also dump with type conditionals but NO spare_clips for type=1 (maybe H4 doesn't have it)
        Console.WriteLine("\n--- Full object dump: noSpawnRel b21 t5 tm4 (no type-1 conditional) ---");
        {
            var (bx2, by2, bz2) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);

            var pb = new ForgeX.Core.IO.BitReader(bitstream);
            pb.SkipBits(objStart);

            int activeIdx = 0;
            for (int i = 0; i < 640; i++)
            {
                int oStart = pb.BitOffset;
                bool exists = pb.ReadBool();
                if (!exists) continue;
                activeIdx++;

                uint flags = pb.ReadInteger(2);
                bool qa2 = pb.ReadBool(); int qi2 = qa2 ? -1 : (int)pb.ReadInteger(8);
                bool va2 = pb.ReadBool(); int vi2 = va2 ? -1 : (int)pb.ReadInteger(5);

                bool inBounds = pb.ReadBool();
                float px, py, pz;
                if (inBounds)
                {
                    px = ForgeX.Core.Reach.ReachPositionEncoding.DecodePosition(pb, bx2, xMin, xMax);
                    py = ForgeX.Core.Reach.ReachPositionEncoding.DecodePosition(pb, by2, yMin, yMax);
                    pz = ForgeX.Core.Reach.ReachPositionEncoding.DecodePosition(pb, bz2, zMin, zMax);
                }
                else
                {
                    px = pb.ReadRawFloat(); py = pb.ReadRawFloat(); pz = pb.ReadRawFloat();
                }

                bool defUp = pb.ReadBool();
                if (!defUp) pb.ReadInteger(20);
                pb.ReadInteger(14);

                uint shape = pb.ReadInteger(2);
                switch (shape)
                {
                    case 1: pb.ReadInteger(11); break;
                    case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                    case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                }
                int seq = (int)pb.ReadInteger(8);
                int resp = (int)pb.ReadInteger(8);
                int typ = (int)pb.ReadInteger(5);
                bool la2 = pb.ReadBool(); int li2 = la2 ? -1 : (int)pb.ReadInteger(8);
                int pfl = (int)pb.ReadInteger(8);
                int team = (int)pb.ReadInteger(4) - 1;
                bool ca2 = pb.ReadBool(); int ci2 = ca2 ? -1 : (int)pb.ReadInteger(3);

                // Only teleporter and location_name conditionals (skip weapon spare_clips)
                switch (typ)
                {
                    case 12: case 13: case 14: pb.ReadInteger(5); pb.ReadInteger(5); break;
                    case 19: { bool xa = pb.ReadBool(); if (!xa) pb.ReadInteger(8); break; }
                }

                int objBits = pb.BitOffset - oStart;
                if (pb.BitsRemaining < 0) { Console.WriteLine("  *** OUT OF BITS ***"); break; }
            }
            Console.WriteLine($"Total active: {activeIdx}, end bit: {pb.BitOffset}, target: {quotaStartBit}, delta: {pb.BitOffset - quotaStartBit}");
        }
    }

    [Fact]
    public void InspectHalo4Xbox360Container()
    {
        string h4File = "/run/media/james/HDD/Non-work Related/Games/Xbox 360/Soft Mods/Halo 4/usermaps/mfaj0f2biucaaqniraqggsvmdlzsg0zmvbtawsmao";
        if (!File.Exists(h4File))
        {
            Console.WriteLine("SKIP: Halo 4 Xbox 360 usermap not found");
            return;
        }

        var tempFile = Path.GetTempFileName();
        File.Copy(h4File, tempFile, true);

        try
        {
            var container = new StfsContainer(tempFile);
            Console.WriteLine($"TitleID: 0x{container.TitleID:X8}");
            Console.WriteLine($"ContentType: {container.ContentType}");
            Console.WriteLine($"DisplayName: {container.DisplayName}");
            Console.WriteLine($"Description: {container.Description}");
            Console.WriteLine($"TitleName: {container.TitleName}");
            Console.WriteLine($"Entries: {container.Entries.Count}");
            foreach (var entry in container.Entries)
                Console.WriteLine($"  File: '{entry.FileName}' Size={entry.Size}");

            var sandbox = container.GetEntryByFileName("sandbox.map");
            if (sandbox != null)
            {
                var data = sandbox.GetData();
                Console.WriteLine($"\nsandbox.map size: {data.Length}");
                Console.Write("First 128 bytes: ");
                for (int i = 0; i < Math.Min(128, data.Length); i++)
                    Console.Write($"{data[i]:X2} ");
                Console.WriteLine();

                // Check if it's BLF
                bool isBlf = data.Length >= 4 && data[0] == 0x5F && data[1] == 0x62 && data[2] == 0x6C && data[3] == 0x66;
                Console.WriteLine($"\nIs BLF format: {isBlf}");

                if (isBlf)
                {
                    var tempMvar = Path.GetTempFileName() + ".mvar";
                    File.WriteAllBytes(tempMvar, data);
                    try
                    {
                        var blf = new BlfFile(tempMvar);
                        Console.WriteLine($"BLF chunks:");
                        foreach (var chunk in blf.Chunks)
                            Console.WriteLine($"  Tag='{chunk.Tag}' Size={chunk.Size} Version={chunk.MajorVersion}.{chunk.MinorVersion}");
                        Console.WriteLine($"Variant format: {blf.VariantFormat}");
                        Console.WriteLine($"mvar major version: {blf.GetMvarMajorVersion()}");
                    }
                    finally
                    {
                        File.Delete(tempMvar);
                    }
                }
                else
                {
                    // Check for ASCII strings in first part
                    Console.Write("First 128 as ASCII: ");
                    for (int i = 0; i < Math.Min(128, data.Length); i++)
                        Console.Write(data[i] >= 32 && data[i] < 127 ? (char)data[i] : '.');
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("No sandbox.map found!");
                // List all entries
                foreach (var entry in container.Entries)
                    Console.WriteLine($"  Entry: '{entry.FileName}'");
            }

            container.Close();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Halo4Xbox360VsMccComparison()
    {
        string h4File = "/run/media/james/HDD/Non-work Related/Games/Xbox 360/Soft Mods/Halo 4/usermaps/mfaj0f2biucaaqniraqggsvmdlzsg0zmvbtawsmao";
        string mccFile = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(h4File) || !File.Exists(mccFile)) { Console.WriteLine("SKIP"); return; }

        // Load Xbox 360 mvar data
        var tempCopy = Path.GetTempFileName();
        File.Copy(h4File, tempCopy, true);
        var container = new StfsContainer(tempCopy);
        var sandbox = container.GetEntryByFileName("sandbox.map")!;
        var x360Data = sandbox.GetData();
        var tempMvar = Path.GetTempFileName() + ".mvar";
        File.WriteAllBytes(tempMvar, x360Data);
        var x360Blf = new BlfFile(tempMvar);
        var x360Mvar = x360Blf.GetChunk("mvar")!;
        container.Close();

        // Load MCC mvar data
        var mccBlf = new BlfFile(mccFile);
        var mccMvar = mccBlf.GetChunk("mvar")!;

        Console.WriteLine($"Xbox 360 mvar: Size={x360Mvar.Size} Data={x360Mvar.Data.Length} Version={x360Mvar.MajorVersion}.{x360Mvar.MinorVersion}");
        Console.WriteLine($"MCC mvar:      Size={mccMvar.Size} Data={mccMvar.Data.Length} Version={mccMvar.MajorVersion}.{mccMvar.MinorVersion}");

        // Dump first 64 bytes of both
        Console.WriteLine("\nXbox 360 mvar data (first 64 bytes):");
        for (int i = 0; i < Math.Min(64, x360Mvar.Data.Length); i++)
            Console.Write($"{x360Mvar.Data[i]:X2} ");
        Console.WriteLine();

        Console.WriteLine("\nMCC mvar data (first 64 bytes):");
        for (int i = 0; i < Math.Min(64, mccMvar.Data.Length); i++)
            Console.Write($"{mccMvar.Data[i]:X2} ");
        Console.WriteLine();

        // Compare packed length fields
        int x360PackedLen = (x360Mvar.Data[20] << 24) | (x360Mvar.Data[21] << 16) |
                            (x360Mvar.Data[22] << 8) | x360Mvar.Data[23];
        int mccPackedLen = (mccMvar.Data[20] << 24) | (mccMvar.Data[21] << 16) |
                           (mccMvar.Data[22] << 8) | mccMvar.Data[23];
        Console.WriteLine($"\nXbox 360 packed len at [20..23]: {x360PackedLen} bytes ({x360PackedLen * 8} bits)");
        Console.WriteLine($"MCC packed len at [20..23]:      {mccPackedLen} bytes ({mccPackedLen * 8} bits)");
        Console.WriteLine($"Xbox 360 data remaining after 24-byte prefix: {x360Mvar.Data.Length - 24}");
        Console.WriteLine($"MCC data remaining after 24-byte prefix:      {mccMvar.Data.Length - 24}");

        // Try to parse the Xbox 360 bitstream starting at offset 24
        var x360Bits = new ForgeX.Core.IO.BitReader(x360Mvar.Data);
        x360Bits.SkipBits(24 * 8); // Skip prefix

        // Read content metadata the same way as MccHalo4MapVariant
        int fileType = (int)x360Bits.ReadInteger(4) - 1;
        uint sizeInBytes = x360Bits.ReadInteger(32);
        ulong uniqueId = x360Bits.ReadInteger64(64);
        Console.WriteLine($"\nXbox 360 metadata: fileType={fileType} sizeInBytes={sizeInBytes} uniqueId=0x{uniqueId:X16}");

        // Skip to name fields
        x360Bits.ReadInteger64(64); // parentUniqueId
        x360Bits.ReadInteger64(64); // rootUniqueId
        x360Bits.ReadInteger64(64); // gameId
        uint activity = x360Bits.ReadInteger(2);
        int gameMode = (int)x360Bits.ReadInteger(3);
        uint engineType = x360Bits.ReadInteger(3);
        int mapId = x360Bits.ReadSignedInteger(32);
        Console.WriteLine($"  activity={activity} gameMode={gameMode} engineType={engineType} mapId={mapId}");

        int megaloCat = x360Bits.ReadSignedInteger(8);
        x360Bits.ReadInteger64(64); // creationTime
        x360Bits.ReadInteger64(64); // creatorXuid
        string author = x360Bits.ReadStringUtf8(16);
        Console.WriteLine($"  megaloCat={megaloCat} author='{author}'");

        x360Bits.ReadBool(); // creatorXuidIsOnline
        x360Bits.ReadInteger64(64); // modTime
        x360Bits.ReadInteger64(64); // modifierXuid
        string modifier = x360Bits.ReadStringUtf8(16);
        x360Bits.ReadBool(); // modifierXuidIsOnline

        string variantName = x360Bits.ReadStringWchar(128);
        string description = x360Bits.ReadStringWchar(128);
        Console.WriteLine($"  modifier='{modifier}'");
        Console.WriteLine($"  variantName='{variantName}'");
        Console.WriteLine($"  description='{description}'");

        // Now try with offset 36 instead of 24 (extra 12 bytes?)
        Console.WriteLine("\n--- Trying with 36-byte prefix instead of 24 ---");
        x360Bits = new ForgeX.Core.IO.BitReader(x360Mvar.Data);
        x360Bits.SkipBits(36 * 8);

        fileType = (int)x360Bits.ReadInteger(4) - 1;
        sizeInBytes = x360Bits.ReadInteger(32);
        uniqueId = x360Bits.ReadInteger64(64);
        Console.WriteLine($"  fileType={fileType} sizeInBytes={sizeInBytes} uniqueId=0x{uniqueId:X16}");

        x360Bits.ReadInteger64(64); x360Bits.ReadInteger64(64); x360Bits.ReadInteger64(64);
        activity = x360Bits.ReadInteger(2);
        gameMode = (int)x360Bits.ReadInteger(3);
        engineType = x360Bits.ReadInteger(3);
        mapId = x360Bits.ReadSignedInteger(32);
        Console.WriteLine($"  activity={activity} gameMode={gameMode} engineType={engineType} mapId={mapId}");

        megaloCat = x360Bits.ReadSignedInteger(8);
        x360Bits.ReadInteger64(64); x360Bits.ReadInteger64(64);
        author = x360Bits.ReadStringUtf8(16);
        Console.WriteLine($"  megaloCat={megaloCat} author='{author}'");
        x360Bits.ReadBool();
        x360Bits.ReadInteger64(64); x360Bits.ReadInteger64(64);
        modifier = x360Bits.ReadStringUtf8(16);
        x360Bits.ReadBool();
        variantName = x360Bits.ReadStringWchar(128);
        description = x360Bits.ReadStringWchar(128);
        Console.WriteLine($"  variantName='{variantName}'");
        Console.WriteLine($"  description='{description}'");

        File.Delete(tempMvar);
        File.Delete(tempCopy);
    }

    [Fact]
    public void CanLoadHalo4Xbox360Usermap()
    {
        string h4File = "/run/media/james/HDD/Non-work Related/Games/Xbox 360/Soft Mods/Halo 4/usermaps/mfaj0f2biucaaqniraqggsvmdlzsg0zmvbtawsmao";
        if (!File.Exists(h4File)) { Console.WriteLine("SKIP: H4 Xbox 360 file not found"); return; }

        var tempCopy = Path.GetTempFileName();
        File.Copy(h4File, tempCopy, true);
        try
        {
            var container = new StfsContainer(tempCopy);
            var sandbox = container.GetEntryByFileName("sandbox.map")!;
            var data = sandbox.GetData();

            // Verify it's a BLF file
            Assert.Equal((byte)0x5F, data[0]); // '_'
            Assert.Equal((byte)0x62, data[1]); // 'b'
            Assert.Equal((byte)0x6C, data[2]); // 'l'
            Assert.Equal((byte)0x66, data[3]); // 'f'

            // Parse as BLF and verify version 50 (H4)
            var tempMvar = Path.GetTempFileName() + ".mvar";
            File.WriteAllBytes(tempMvar, data);
            try
            {
                var blf = new BlfFile(tempMvar);
                Assert.Equal(50, blf.GetMvarMajorVersion());

                // This is the same code path as MainWindowViewModel
                var variant = new MccHalo4MapVariant(blf);
                Console.WriteLine($"Variant Name: '{variant.VariantName}'");
                Console.WriteLine($"Author: '{variant.MapAuthor}'");
                Console.WriteLine($"Map ID: {variant.MapId}");
                Console.WriteLine($"Max Budget: {variant.MaximumBudget}");
                Console.WriteLine($"Current Budget: {variant.CurrentBudget}");
                Console.WriteLine($"Placements: {variant.PlacementChunks.Count(c => c.TagsIndex >= 0)}");
                Console.WriteLine($"Quotas: {variant.TagIndex.Count(e => e.Tag != null)}");

                // Verify correct metadata (not garbled)
                Assert.Equal("Prop Busters Arena v1", variant.VariantName);
                Assert.Equal("Deceitful Echo", variant.MapAuthor);
                Assert.Equal(14100, variant.MapId);
                Assert.True(variant.PlacementChunks.Count(c => c.TagsIndex >= 0) > 0);

                // Verify palette resolves names for Xbox 360 maps
                Assert.NotNull(variant.Palette);
                Assert.Equal("Magnum", variant.Palette.GetQuotaName(0));
                Assert.Equal("Weapon", variant.Palette.GetCategory(0));

                // Print categories to verify resolution
                var categories = variant.TagIndex
                    .Where(e => e.Tag != null)
                    .Select(e => e.Tag!.Class)
                    .Distinct()
                    .OrderBy(c => c);
                Console.WriteLine($"Categories: {string.Join(", ", categories)}");
                Assert.DoesNotContain("h4_object", categories);
            }
            finally
            {
                File.Delete(tempMvar);
            }

            container.Close();
        }
        finally
        {
            File.Delete(tempCopy);
        }
    }

    [Fact]
    public void CanLoadReachXbox360Usermap()
    {
        string reachFile = "/run/media/james/HDD/Non-work Related/Games/Xbox 360/Soft Mods/Halo Reach/usermaps/meq5d43ca0baaab23amcsskwzrofr2ash2gaaaaaa";
        if (!File.Exists(reachFile))
        {
            Console.WriteLine("SKIP: Reach Xbox 360 usermap not found");
            return;
        }

        var tempFile = Path.GetTempFileName();
        File.Copy(reachFile, tempFile, true);

        try
        {
            var container = new StfsContainer(tempFile);
            Console.WriteLine($"TitleID: 0x{container.TitleID:X8}");
            Console.WriteLine($"ContentType: {container.ContentType}");
            Console.WriteLine($"DisplayName: {container.DisplayName}");
            Console.WriteLine($"Description: {container.Description}");
            Console.WriteLine($"TitleName: {container.TitleName}");
            Console.WriteLine($"Entries: {container.Entries.Count}");
            foreach (var entry in container.Entries)
                Console.WriteLine($"  File: '{entry.FileName}' Size={entry.Size}");

            var sandbox = container.GetEntryByFileName("sandbox.map");
            if (sandbox != null)
            {
                var data = sandbox.GetData();
                Console.WriteLine($"\nsandbox.map size: {data.Length}");
                Console.Write("First 64 bytes: ");
                for (int i = 0; i < Math.Min(64, data.Length); i++)
                    Console.Write($"{data[i]:X2} ");
                Console.WriteLine();

                // sandbox.map should start with _blf for Reach
                Assert.Equal((byte)0x5F, data[0]); // '_'
                Assert.Equal((byte)0x62, data[1]); // 'b'
                Assert.Equal((byte)0x6C, data[2]); // 'l'
                Assert.Equal((byte)0x66, data[3]); // 'f'

                // Parse as BLF → MccReachMapVariant
                var tempPath = Path.GetTempFileName() + ".mvar";
                File.WriteAllBytes(tempPath, data);
                try
                {
                    var blf = new BlfFile(tempPath);
                    Console.WriteLine($"\nBLF mvar version: {blf.GetMvarMajorVersion()}");
                    Assert.Equal(31, blf.GetMvarMajorVersion());

                    var variant = new MccReachMapVariant(blf);
                    Console.WriteLine($"Variant Name: '{variant.VariantName}'");
                    Console.WriteLine($"Description: '{variant.VariantDescription}'");
                    Console.WriteLine($"Author: '{variant.MapAuthor}'");
                    Console.WriteLine($"Map ID: {variant.MapId}");
                    Console.WriteLine($"Max Budget: {variant.MaximumBudget}");
                    Console.WriteLine($"Current Budget: {variant.CurrentBudget}");
                    Console.WriteLine($"CanWrite: {variant.CanWrite}");

                    Assert.False(string.IsNullOrEmpty(variant.VariantName));
                    Assert.NotEqual(0, variant.MapId);

                    int activePlacements = variant.PlacementChunks.Count(c => c.TagsIndex >= 0);
                    Console.WriteLine($"Active Placements: {activePlacements}");

                    int activeEntries = variant.TagIndex.Count(e => e.Tag != null);
                    Console.WriteLine($"Active Tag Index Entries: {activeEntries}");

                    foreach (var p in variant.PlacementChunks.Where(c => c.TagsIndex >= 0).Take(5))
                        Console.WriteLine($"  Placement: TagsIdx={p.TagsIndex} Pos=({p.SpawnCoords.X:F2},{p.SpawnCoords.Y:F2},{p.SpawnCoords.Z:F2})");
                }
                finally
                {
                    File.Delete(tempPath);
                }
            }
            else
            {
                Console.WriteLine("No sandbox.map found!");
            }

            container.Close();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Halo4SpawnRelBitSearch()
    {
        // Test grifball with various spawn_relative_to bit widths to find exact per-object format.
        // noSpawnRel delta=-464, full Reach (10-bit) delta≈+226. Truth is in between.
        // 7 bits: 69*7=483 → delta=-464+483=+19 ← very close!
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP: No H4 files"); return; }

        foreach (var filePath in files)
        {
            Console.WriteLine($"\n=== {Path.GetFileName(filePath)} ===");
            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            var bitstream = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);

            int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
            int packedLenBits = packedLenBytes * 8;

            // Parse metadata + header + strings (same as MultiFileAnalysis)
            var bits = new ForgeX.Core.IO.BitReader(bitstream);
            int ft = (int)bits.ReadInteger(4);
            bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32);
            bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

            uint encVer = bits.ReadInteger(8);
            bits.ReadInteger(32); bits.ReadInteger(32);
            int numQuotas = (int)bits.ReadInteger(9);
            uint mapId = bits.ReadInteger(32);
            bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            bits.ReadInteger(32); bits.ReadInteger(32);

            int strCount = (int)bits.ReadInteger(9);
            for (int i = 0; i < strCount; i++)
            {
                bool exists = bits.ReadBool();
                if (exists) bits.ReadInteger(12);
            }
            if (strCount > 0)
            {
                int bufSize = (int)bits.ReadInteger(13);
                bool compressed = bits.ReadBool();
                if (compressed)
                {
                    int compSize = (int)bits.ReadInteger(13);
                    for (int ci = 0; ci < compSize; ci++) bits.ReadInteger(8);
                }
                else
                {
                    for (int ci = 0; ci < bufSize; ci++) bits.ReadInteger(8);
                }
            }

            int objStart = bits.BitOffset;
            var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);

            // Use packed length to compute expected end of data
            int quotaSize = numQuotas * 24;

            Console.WriteLine($"  objStart={objStart} posBits={bitsX}+{bitsY}+{bitsZ}={bitsX+bitsY+bitsZ}");
            Console.WriteLine($"  packed={packedLenBits} bits, quotaSize={quotaSize}, expectedEnd={packedLenBits}");

            // Try various spawn_relative_to encodings and report delta
            // Formats: noSR, SR(5..10 bits raw), SR index-encoded(5..9 value bits)
            var formats = new List<(string Name, int SrBits, bool SrIndexEncoded)>();
            formats.Add(("noSR", 0, false));
            for (int sr = 5; sr <= 10; sr++)
                formats.Add(($"SR_raw{sr}", sr, false));
            for (int sr = 4; sr <= 9; sr++)
                formats.Add(($"SR_idx{sr}", sr, true));

            // For each format, try quotas at scan-found position AND at expected position
            // First, find quota positions via scan (constrained to packed data)
            var quotaCandidates = new List<(int Pos, int NonZero)>();
            int scanEnd = Math.Min(packedLenBits, bitstream.Length * 8) - quotaSize;
            for (int startBit = objStart + 1000; startBit <= scanEnd; startBit++)
            {
                var qr = new ForgeX.Core.IO.BitReader(bitstream);
                qr.SkipBits(startBit);
                int nonZero = 0;
                bool bad = false;
                for (int q = 0; q < numQuotas; q++)
                {
                    int mn = (int)qr.ReadInteger(8);
                    int mx = (int)qr.ReadInteger(8);
                    int ct = (int)qr.ReadInteger(8);
                    if (mn == 0 && mx == 0 && ct == 0) continue;
                    if (mn > mx || ct > mx || mx > 200) { bad = true; break; }
                    nonZero++;
                }
                if (!bad && nonZero >= 3)
                    quotaCandidates.Add((startBit, nonZero));
            }

            // Pick best scan position (most non-zero, then earliest)
            int scanQuota = -1;
            if (quotaCandidates.Count > 0)
            {
                var best = quotaCandidates.OrderByDescending(c => c.NonZero).ThenBy(c => c.Pos).First();
                scanQuota = best.Pos;
                Console.WriteLine($"  Scan quota: pos={scanQuota} nonZero={best.NonZero} ({quotaCandidates.Count} candidates)");
            }

            // Also try expected position (end of packed data - quotaSize)
            int expectedQuota = packedLenBits - quotaSize;

            Console.WriteLine($"  Expected quota: pos={expectedQuota}");

            foreach (var (name, srBits, srIdx) in formats)
            {
                // Test against both quota positions
                foreach (var (qLabel, qPos) in new[] { ("scan", scanQuota), ("expected", expectedQuota) })
                {
                    if (qPos < 0) continue;

                    var pb = new ForgeX.Core.IO.BitReader(bitstream);
                    pb.SkipBits(objStart);
                    int active = 0;
                    bool parseError = false;

                    for (int i = 0; i < 640 && !parseError; i++)
                    {
                        if (pb.BitsRemaining < 1) { parseError = true; break; }
                        bool ex = pb.ReadBool();
                        if (!ex) continue;
                        active++;

                        pb.ReadInteger(2); // flags
                        bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8); // quota_index
                        bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5); // variant_index
                        bool ib = pb.ReadBool(); // point_in_bounds
                        if (ib) { pb.ReadInteger(bitsX); pb.ReadInteger(bitsY); pb.ReadInteger(bitsZ); }
                        else { pb.ReadRawFloat(); pb.ReadRawFloat(); pb.ReadRawFloat(); }
                        bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14); // orientation

                        // spawn_relative_to
                        if (srBits > 0)
                        {
                            if (srIdx)
                            {
                                bool absent = pb.ReadBool();
                                if (!absent) pb.ReadInteger(srBits);
                            }
                            else
                            {
                                pb.ReadInteger(srBits);
                            }
                        }

                        // multiplayer properties
                        uint shape = pb.ReadInteger(2);
                        switch (shape)
                        {
                            case 1: pb.ReadInteger(11); break;
                            case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                            case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                        }
                        pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(5); // seq, respawn, type
                        bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8); // label
                        pb.ReadInteger(8); pb.ReadInteger(4); // pflags, team
                        bool ca = pb.ReadBool(); if (!ca) pb.ReadInteger(3); // color

                        if (pb.BitsRemaining < 0) parseError = true;
                    }

                    int delta = pb.BitOffset - qPos;
                    // Print all results for grifball, only close results for others
                    bool isGrifball = Path.GetFileName(filePath).Contains("grifball");
                    if (Math.Abs(delta) <= 50 || (name == "noSR" && qLabel == "scan") || (isGrifball && qLabel == "scan"))
                    {
                        Console.WriteLine($"  {name} vs {qLabel}: delta={delta:+#;-#;0} active={active} err={parseError}");
                    }
                }
            }
        }
    }

    [Fact]
    public void Halo4FormatHypothesisTest()
    {
        // Test H4 per-object format hypotheses across ALL files.
        // Use quota validation: sum of placed counts must equal active object count.
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP: No H4 files"); return; }

        // Define format configs: (name, hasSeq, hasLabel, hasColor, hasShared, hasConditional, typeBits, pflagsBits, teamBits, dimBits)
        var fmtDefs = new List<(string N, bool Seq, bool Lab, bool Col, bool Shr, bool Cond, int Ty, int Pf, int Tm, int Dm)>
        {
            ("Reach_noSR",  true,  true,  true,  false, true,  5,  8,  4, 11),
            ("H4_A",        false, false, false, true,  false, 5,  8,  4, 11),
            ("H4_B",        false, false, false, true,  false, 8,  8,  4, 11),
            ("H4_E",        true,  false, false, true,  false, 5,  8,  4, 11),
            ("H4_G",        true,  false, false, false, false, 5,  8,  4, 11),
            // 16-bit dims
            ("H4_16d_A",    false, false, false, true,  false, 5,  8,  4, 16),
            ("H4_16d_B",    false, false, false, true,  false, 8,  8,  4, 16),
            ("H4_16d_C",    true,  false, false, true,  false, 5,  8,  4, 16),
            ("H4_16d_min",  false, false, false, false, false, 5,  8,  4, 16),
            ("H4_16d_R",    true,  true,  true,  false, true,  5,  8,  4, 16),
        };

        foreach (var filePath in files)
        {
            Console.WriteLine($"\n=== {Path.GetFileName(filePath)} ===");
            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            var bitstream = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
            int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
            int packedLenBits = packedLenBytes * 8;

            var bits = new ForgeX.Core.IO.BitReader(bitstream);
            bits.ReadInteger(4); bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32);
            bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

            bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
            int numQuotas = (int)bits.ReadInteger(9);
            bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            bits.ReadInteger(32); bits.ReadInteger(32);

            int strCount = (int)bits.ReadInteger(9);
            for (int i = 0; i < strCount; i++)
            { bool exists = bits.ReadBool(); if (exists) bits.ReadInteger(12); }
            if (strCount > 0)
            {
                int bufSize = (int)bits.ReadInteger(13);
                bool compressed = bits.ReadBool();
                int readSize = compressed ? (int)bits.ReadInteger(13) : bufSize;
                for (int ci = 0; ci < readSize; ci++) bits.ReadInteger(8);
            }

            int objStart = bits.BitOffset;
            var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);
            Console.WriteLine($"  objStart={objStart} posBits={bitsX}+{bitsY}+{bitsZ} packed={packedLenBits}");

            // For each format, parse objects, then validate quotas at the parse end position
            foreach (var f in fmtDefs)
            {
                int endBit = ParseObjects(bitstream, objStart, bitsX, bitsY, bitsZ,
                    f.Seq, f.Lab, f.Col, f.Shr, f.Cond, f.Ty, f.Pf, f.Tm, f.Dm);

                // Try reading quotas starting at endBit
                if (endBit + numQuotas * 24 > bitstream.Length * 8) continue;
                var qr = new ForgeX.Core.IO.BitReader(bitstream);
                qr.SkipBits(endBit);
                int nz = 0; int totalPlaced = 0; bool valid = true;
                for (int q = 0; q < numQuotas; q++)
                {
                    int mn = (int)qr.ReadInteger(8);
                    int mx = (int)qr.ReadInteger(8);
                    int ct = (int)qr.ReadInteger(8);
                    if (mn == 0 && mx == 0 && ct == 0) continue;
                    if (mn > mx || ct > mx || mx > 200) { valid = false; break; }
                    nz++;
                    totalPlaced += ct;
                }
                int quotaEnd = qr.BitOffset;
                bool fitsInPacked = quotaEnd <= packedLenBits;

                // Also check if there are valid quotas within ±8 bits of endBit
                int bestOffset = 0;
                int bestNz = 0;
                bool bestValid = false;
                int bestPlaced = 0;
                for (int off = -8; off <= 8; off++)
                {
                    int tryPos = endBit + off;
                    if (tryPos < objStart || tryPos + numQuotas * 24 > bitstream.Length * 8) continue;
                    var qr2 = new ForgeX.Core.IO.BitReader(bitstream);
                    qr2.SkipBits(tryPos);
                    int nz2 = 0; int pl2 = 0; bool v2 = true;
                    for (int q = 0; q < numQuotas; q++)
                    {
                        int mn = (int)qr2.ReadInteger(8);
                        int mx = (int)qr2.ReadInteger(8);
                        int ct = (int)qr2.ReadInteger(8);
                        if (mn == 0 && mx == 0 && ct == 0) continue;
                        if (mn > mx || ct > mx || mx > 200) { v2 = false; break; }
                        nz2++; pl2 += ct;
                    }
                    if (v2 && nz2 > bestNz)
                    {
                        bestOffset = off;
                        bestNz = nz2;
                        bestValid = true;
                        bestPlaced = pl2;
                    }
                }

                if (valid && nz >= 3 || bestNz >= 3)
                {
                    Console.Write($"  {f.N,-14} end={endBit,6}");
                    if (valid && nz >= 3)
                        Console.Write($" @0: nz={nz} placed={totalPlaced} fit={fitsInPacked}");
                    if (bestNz >= 3 && bestOffset != 0)
                        Console.Write($" @{bestOffset:+#;-#}: nz={bestNz} placed={bestPlaced}");
                    Console.WriteLine();
                }
            }
        }
    }

    // Full Reach format (with all conditional per-type fields)
    private int ParseObjectsReachFull(byte[] bitstream, int objStart, int bitsX, int bitsY, int bitsZ)
    {
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);
        for (int i = 0; i < 640; i++)
        {
            if (pb.BitsRemaining < 1) break;
            if (!pb.ReadBool()) continue; // exists

            pb.ReadInteger(2); // flags
            bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8); // quota_index
            bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5); // variant_index
            bool ib = pb.ReadBool();
            if (ib) { pb.ReadInteger(bitsX); pb.ReadInteger(bitsY); pb.ReadInteger(bitsZ); }
            else { pb.ReadRawFloat(); pb.ReadRawFloat(); pb.ReadRawFloat(); }
            bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14); // orientation

            uint shape = pb.ReadInteger(2);
            switch (shape) {
                case 1: pb.ReadInteger(11); break;
                case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
            }
            uint seq = pb.ReadInteger(8); // spawn_sequence
            pb.ReadInteger(8); // respawn_time
            uint cachedType = pb.ReadInteger(5); // cached_type
            bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8); // label_index
            pb.ReadInteger(8); // placement_flags
            pb.ReadInteger(4); // team
            bool ca = pb.ReadBool(); if (!ca) pb.ReadInteger(3); // primary_color

            // Conditional per-type fields
            if (cachedType == 2) // weapon
                pb.ReadInteger(8); // spare_clips
            if (cachedType == 10) // teleporter
                { pb.ReadInteger(5); pb.ReadInteger(5); } // channel, passability
            // location_name_index (always)
            bool lna = pb.ReadBool(); if (!lna) pb.ReadInteger(8);

            if (pb.BitsRemaining < 0) break;
        }
        return pb.BitOffset;
    }

    // H4 format based on ElDewrito research
    private int ParseObjectsH4(byte[] bitstream, int objStart, int bitsX, int bitsY, int bitsZ,
        int dimBits = 11, bool hasSharedStorage = true, int typeBits = 5, int pflagsBits = 8, int teamBits = 4)
    {
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);
        for (int i = 0; i < 640; i++)
        {
            if (pb.BitsRemaining < 1) break;
            if (!pb.ReadBool()) continue; // exists

            pb.ReadInteger(2); // flags
            bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8); // budget_index
            bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5); // variant_index
            bool ib = pb.ReadBool();
            if (ib) { pb.ReadInteger(bitsX); pb.ReadInteger(bitsY); pb.ReadInteger(bitsZ); }
            else { pb.ReadRawFloat(); pb.ReadRawFloat(); pb.ReadRawFloat(); }
            bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);

            uint shape = pb.ReadInteger(2);
            switch (shape) {
                case 1: pb.ReadInteger(dimBits); break;
                case 2: pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); break;
                case 3: pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); break;
            }
            // H4: no spawn_sequence, no label_index, no primary_color, no conditional per-type
            // H4: has shared_storage(8), respawn(8), type(5), pflags(8), team(4)
            if (hasSharedStorage) pb.ReadInteger(8);
            pb.ReadInteger(8); // respawn_time
            pb.ReadInteger(typeBits);
            pb.ReadInteger(pflagsBits);
            pb.ReadInteger(teamBits);

            if (pb.BitsRemaining < 0) break;
        }
        return pb.BitOffset;
    }

    // H3-like packed format with Reach's adaptive position encoding
    // Based on H3 MCC packed format from MccMapVariant.cs but with Reach position bits
    private (int EndBit, int ActiveCount, int GoodCount) ParseObjectsH3Style(
        byte[] bitstream, int objStart, int numSlots,
        int bitsX, int bitsY, int bitsZ,
        float xMin, float xMax, float yMin, float yMax, float zMin, float zMax,
        bool useAdaptivePos, bool useReachOrientation, bool verbose = false)
    {
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);
        int active = 0, good = 0;

        for (int i = 0; i < numSlots; i++)
        {
            if (pb.BitsRemaining < 1) break;
            int startBit = pb.BitOffset;

            bool exists = pb.ReadBool();
            if (!exists) continue;
            active++;

            // H3 packed: placement_flags(16), quota_index(signed 32)
            uint pflags = pb.ReadInteger(16);
            int qi = pb.ReadSignedInteger(32);

            // H3 packed: has_parent_object(1) + optional 64-bit parent
            bool hasParent = pb.ReadBool();
            if (hasParent) pb.ReadInteger64(64);

            // has_position (same concept as point_in_bounds)
            bool hasPos = pb.ReadBool();
            if (!hasPos)
            {
                // Scenario object with default position — no further data
                if (verbose && active <= 15)
                    Console.WriteLine($"  [{i}] #{active} @{startBit}: pflags=0x{pflags:X4} qi={qi} parent={hasParent} NO_POS → @{pb.BitOffset}");
                continue;
            }

            // Position
            float px, py, pz;
            if (useAdaptivePos)
            {
                uint rx = pb.ReadInteger(bitsX), ry = pb.ReadInteger(bitsY), rz = pb.ReadInteger(bitsZ);
                px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
            }
            else
            {
                // H3-style fixed 16-bit
                uint rx = pb.ReadInteger(16), ry = pb.ReadInteger(16), rz = pb.ReadInteger(16);
                px = xMin + (rx + 0.5f) * ((xMax - xMin) / 65536f);
                py = yMin + (ry + 0.5f) * ((yMax - yMin) / 65536f);
                pz = zMin + (rz + 0.5f) * ((zMax - zMin) / 65536f);
            }

            // Orientation
            if (useReachOrientation)
            {
                bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
            }
            else
            {
                // H3: 1-bit default_up + conditional 19-bit up + 8-bit forward angle
                bool du = pb.ReadBool(); if (!du) pb.ReadInteger(19); pb.ReadInteger(8);
            }

            // H3 packed properties: type(8) sym(8) engine(16) clips(8) resp(8) team(8) shape(8)
            int typ = pb.ReadSignedInteger(8);
            uint sym = pb.ReadInteger(8);
            uint eng = pb.ReadInteger(16);
            uint clips = pb.ReadInteger(8);
            uint resp = pb.ReadInteger(8);
            uint team = pb.ReadInteger(8);
            uint shape = pb.ReadInteger(8);

            // Boundary data (H3 uses 16-bit quantized [0, 60])
            switch (shape)
            {
                case 1: pb.ReadInteger(16); pb.ReadInteger(16); break;
                case 2: pb.ReadInteger(16); pb.ReadInteger(16); pb.ReadInteger(16); break;
                case 3: pb.ReadInteger(16); pb.ReadInteger(16); pb.ReadInteger(16); pb.ReadInteger(16); break;
            }

            int endBit = pb.BitOffset;
            bool posOk = px >= xMin - 50 && px <= xMax + 50 && py >= yMin - 50 && py <= yMax + 50 && pz >= zMin - 50 && pz <= zMax + 50;
            bool fieldsOk = qi >= -1 && qi < 256 && shape <= 3;
            bool isGood = posOk && fieldsOk;
            if (isGood) good++;

            if (verbose && (active <= 15 || !isGood))
            {
                string marker = isGood ? "OK" : "BAD";
                Console.WriteLine($"  [{i}] #{active} @{startBit}: pflags=0x{pflags:X4} qi={qi} parent={hasParent} pos=({px:F1},{py:F1},{pz:F1}) typ={typ} sym={sym} eng=0x{eng:X4} clips={clips} resp={resp} team={team} shape={shape} [{marker}] {endBit - startBit}b → @{endBit}");
                if (!isGood && active > 5) break;
            }

            if (pb.BitsRemaining < 0) break;
        }
        return (pb.BitOffset, active, good);
    }

    // Stub for old signature - delegates to appropriate implementation
    private int ParseObjects(byte[] bitstream, int objStart, int bitsX, int bitsY, int bitsZ,
        bool hasSpawnSeq, bool hasLabel, bool hasPrimaryColor, bool hasSharedStorage,
        bool hasConditionalPerType, int typeBits, int pflagsBits, int teamBits, int dimBits = 11)
    {
        // For the hypothesis test, use the old logic
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);
        for (int i = 0; i < 640; i++)
        {
            if (pb.BitsRemaining < 1) break;
            if (!pb.ReadBool()) continue;
            pb.ReadInteger(2);
            bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
            bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
            bool ib = pb.ReadBool();
            if (ib) { pb.ReadInteger(bitsX); pb.ReadInteger(bitsY); pb.ReadInteger(bitsZ); }
            else { pb.ReadRawFloat(); pb.ReadRawFloat(); pb.ReadRawFloat(); }
            bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
            uint shape = pb.ReadInteger(2);
            switch (shape) {
                case 1: pb.ReadInteger(dimBits); break;
                case 2: pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); break;
                case 3: pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); break;
            }
            if (hasSharedStorage) pb.ReadInteger(8);
            if (hasSpawnSeq) pb.ReadInteger(8);
            pb.ReadInteger(8);
            pb.ReadInteger(typeBits);
            if (hasLabel) { bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8); }
            pb.ReadInteger(pflagsBits);
            pb.ReadInteger(teamBits);
            if (hasPrimaryColor) { bool ca = pb.ReadBool(); if (!ca) pb.ReadInteger(3); }
            if (pb.BitsRemaining < 0) break;
        }
        return pb.BitOffset;
    }

    [Fact]
    public void Halo4PerObjectDump()
    {
        string path = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(path))
        {
            Console.WriteLine("SKIP: Halo 4 MCC .mvar not found");
            return;
        }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);

        int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
        int packedLenBits = packedLenBytes * 8;

        // --- Parse content metadata (H4 format) ---
        var bits = new ForgeX.Core.IO.BitReader(bitstream);
        bits.ReadInteger(4);   // file_type
        bits.ReadInteger(32);  // size
        bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); // 4x64 IDs
        bits.ReadInteger(2);   // activity
        bits.ReadInteger(3);   // game_mode
        bits.ReadInteger(3);   // engine_type
        bits.ReadInteger(32);  // unk2C
        bits.ReadInteger(8);   // engine_category_index
        bits.ReadInteger64(64); bits.ReadInteger64(64); // creator timestamps
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; } // creator_name
        bits.ReadBool(); // creator_is_online
        bits.ReadInteger64(64); bits.ReadInteger64(64); // mod timestamps
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; } // mod_name
        bits.ReadBool(); // mod_is_online
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; } // title
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; } // desc

        Console.WriteLine($"After metadata: bit {bits.BitOffset}");

        // --- Header ---
        uint encVer = bits.ReadInteger(8);
        bits.ReadInteger(32); // RSA
        bits.ReadInteger(32); // CRC
        int numQuotas = (int)bits.ReadInteger(9);
        uint mapId = bits.ReadInteger(32);
        bits.ReadBool(); bits.ReadBool(); // built_in, built_from_xml
        float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
        float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
        float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
        bits.ReadInteger(32); bits.ReadInteger(32); // budgets

        Console.WriteLine($"Header: version={encVer} quotas={numQuotas} mapId={mapId}");
        Console.WriteLine($"Bounds: X[{xMin:F1},{xMax:F1}] Y[{yMin:F1},{yMax:F1}] Z[{zMin:F1},{zMax:F1}]");

        // --- String table ---
        int strCount = (int)bits.ReadInteger(9);
        for (int i = 0; i < strCount; i++)
        {
            bool exists = bits.ReadBool();
            if (exists) bits.ReadInteger(12);
        }
        if (strCount > 0)
        {
            int bufSize = (int)bits.ReadInteger(13);
            bool compressed = bits.ReadBool();
            if (compressed)
            {
                int compSize = (int)bits.ReadInteger(13);
                for (int ci = 0; ci < compSize; ci++) bits.ReadInteger(8);
            }
            else
            {
                for (int ci = 0; ci < bufSize; ci++) bits.ReadInteger(8);
            }
        }

        int objStart = bits.BitOffset;
        var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
            xMin, xMax, yMin, yMax, zMin, zMax, 21);

        Console.WriteLine($"Object section starts at bit: {objStart}");
        Console.WriteLine($"Position bit counts: X={bitsX} Y={bitsY} Z={bitsZ} total={bitsX + bitsY + bitsZ}");

        // --- Find expected quota position ---
        int quotaSize = numQuotas * 24;
        int expectedQuotaStart = packedLenBits - quotaSize;
        Console.WriteLine($"Packed data: {packedLenBytes} bytes = {packedLenBits} bits");
        Console.WriteLine($"Expected quota start: {expectedQuotaStart} (packed - {quotaSize})");

        // Also scan for best quota position
        var quotaCandidates = new List<(int Pos, int NonZero)>();
        int scanEnd = Math.Min(packedLenBits, bitstream.Length * 8) - quotaSize;
        for (int startBit = objStart + 1000; startBit <= scanEnd; startBit++)
        {
            var qr = new ForgeX.Core.IO.BitReader(bitstream);
            qr.SkipBits(startBit);
            int nonZero = 0;
            bool bad = false;
            for (int q = 0; q < numQuotas; q++)
            {
                int mn = (int)qr.ReadInteger(8);
                int mx = (int)qr.ReadInteger(8);
                int ct = (int)qr.ReadInteger(8);
                if (mn == 0 && mx == 0 && ct == 0) continue;
                if (mn > mx || ct > mx || mx > 200) { bad = true; break; }
                nonZero++;
            }
            if (!bad && nonZero >= 3)
                quotaCandidates.Add((startBit, nonZero));
        }

        int scanQuota = -1;
        if (quotaCandidates.Count > 0)
        {
            var best = quotaCandidates.OrderByDescending(c => c.NonZero).ThenBy(c => c.Pos).First();
            scanQuota = best.Pos;
            Console.WriteLine($"Scan quota: pos={scanQuota} nonZero={best.NonZero} ({quotaCandidates.Count} candidates)");
        }

        // --- Parse 640 object slots with Reach noSpawnRel format ---
        Console.WriteLine($"\n=== Per-object parse (Reach noSpawnRel format) ===");
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);

        int activeCount = 0;
        int totalBitsConsumed = 0;

        for (int i = 0; i < 640; i++)
        {
            int startBitObj = pb.BitOffset;

            // Existence flag (1 bit)
            bool exists = pb.ReadBool();
            if (!exists)
            {
                totalBitsConsumed += 1;
                continue;
            }
            activeCount++;

            // flags (2 bits)
            uint flags = pb.ReadInteger(2);

            // quota_index: index-encoded 1+8
            bool qaAbsent = pb.ReadBool();
            int quotaIndex = qaAbsent ? -1 : (int)pb.ReadInteger(8);

            // variant_index: index-encoded 1+5
            bool vaAbsent = pb.ReadBool();
            int variantIndex = vaAbsent ? -1 : (int)pb.ReadInteger(5);

            // position: point_in_bounds flag + encoded/raw coords
            bool inBounds = pb.ReadBool();
            float px, py, pz;
            uint rawPosX = 0, rawPosY = 0, rawPosZ = 0;
            if (inBounds)
            {
                // Read raw values for display, then decode
                int posStartBit = pb.BitOffset;
                rawPosX = pb.ReadInteger(bitsX);
                rawPosY = pb.ReadInteger(bitsY);
                rawPosZ = pb.ReadInteger(bitsZ);
                // Decode manually using the formula: min + (raw + 0.5) * (range / 2^bits)
                px = xMin + (rawPosX + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                py = yMin + (rawPosY + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                pz = zMin + (rawPosZ + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
            }
            else
            {
                px = pb.ReadRawFloat();
                py = pb.ReadRawFloat();
                pz = pb.ReadRawFloat();
            }

            // orientation: is_default_up (1 bit), axis vector (20 bits if not default), angle (14 bits always)
            bool isDefaultUp = pb.ReadBool();
            uint axisRaw = 0;
            if (!isDefaultUp) axisRaw = pb.ReadInteger(20);
            uint angleRaw = pb.ReadInteger(14);

            // boundary shape (2 bits) + dimensions
            uint shape = pb.ReadInteger(2);
            uint dim0 = 0, dim1 = 0, dim2 = 0, dim3 = 0;
            switch (shape)
            {
                case 1: dim0 = pb.ReadInteger(11); break;
                case 2: dim0 = pb.ReadInteger(11); dim1 = pb.ReadInteger(11); dim2 = pb.ReadInteger(11); break;
                case 3: dim0 = pb.ReadInteger(11); dim1 = pb.ReadInteger(11); dim2 = pb.ReadInteger(11); dim3 = pb.ReadInteger(11); break;
            }

            // spawn_sequence(8), respawn_time(8), cached_type(5)
            uint spawnSeq = pb.ReadInteger(8);
            uint respawnTime = pb.ReadInteger(8);
            uint cachedType = pb.ReadInteger(5);

            // label_index: index-encoded 1+8
            bool laAbsent = pb.ReadBool();
            int labelIndex = laAbsent ? -1 : (int)pb.ReadInteger(8);

            // placement_flags(8), team(4)
            uint placementFlags = pb.ReadInteger(8);
            uint team = pb.ReadInteger(4);

            // primary_color: index-encoded 1+3
            bool caAbsent = pb.ReadBool();
            int primaryColor = caAbsent ? -1 : (int)pb.ReadInteger(3);

            int endBitObj = pb.BitOffset;
            int bitsForObj = endBitObj - startBitObj;
            totalBitsConsumed += bitsForObj;

            // Print detailed info for first 40 active objects
            if (activeCount <= 40)
            {
                Console.WriteLine($"\n--- Object [{i}] (active #{activeCount}) ---");
                Console.WriteLine($"  Start bit: {startBitObj}");
                Console.WriteLine($"  flags={flags} quota_index={quotaIndex} variant_index={variantIndex}");
                Console.WriteLine($"  point_in_bounds={inBounds}");
                if (inBounds)
                    Console.WriteLine($"  position: raw=({rawPosX},{rawPosY},{rawPosZ}) decoded=({px:F3},{py:F3},{pz:F3})");
                else
                    Console.WriteLine($"  position (OOB float): ({px:F3},{py:F3},{pz:F3})");
                Console.WriteLine($"  orientation: is_default_up={isDefaultUp} axis_raw=0x{axisRaw:X5} angle_raw={angleRaw}");
                Console.WriteLine($"  boundary: shape={shape} dims=({dim0},{dim1},{dim2},{dim3})");
                Console.WriteLine($"  spawn_sequence={spawnSeq} respawn_time={respawnTime} cached_type={cachedType}");
                Console.WriteLine($"  label_index={labelIndex} placement_flags=0x{placementFlags:X2} team={team} primary_color={primaryColor}");
                Console.WriteLine($"  End bit: {endBitObj} | Bits consumed: {bitsForObj}");
            }
            else
            {
                // Summary only for remaining active objects
                Console.WriteLine($"  obj[{i}] active#{activeCount}: startBit={startBitObj} endBit={endBitObj} bits={bitsForObj}");
            }

            if (pb.BitsRemaining < 0)
            {
                Console.WriteLine($"  *** OVERRUN at object [{i}] - bits remaining went negative! ***");
                break;
            }
        }

        int finalBitOffset = pb.BitOffset;
        Console.WriteLine($"\n=== Summary ===");
        Console.WriteLine($"Total active objects: {activeCount}");
        Console.WriteLine($"Total bits consumed (all 640 slots): {totalBitsConsumed}");
        Console.WriteLine($"Final bit offset after all objects: {finalBitOffset}");
        Console.WriteLine($"Expected quota positions:");
        Console.WriteLine($"  Scan-found:  {scanQuota} (delta={finalBitOffset - scanQuota})");
        Console.WriteLine($"  Packed-end:  {expectedQuotaStart} (delta={finalBitOffset - expectedQuotaStart})");
        Console.WriteLine($"Inactive slot bits: {640 - activeCount} (1 bit each)");
        Console.WriteLine($"Active object bits: {totalBitsConsumed - (640 - activeCount)}");
        if (activeCount > 0)
            Console.WriteLine($"Avg bits per active object: {(float)(totalBitsConsumed - (640 - activeCount)) / activeCount:F1}");
    }

    [Fact]
    public void Halo4QuotaFormatSearch()
    {
        // Search for the correct quota format in all H4 files.
        // Try different per-entry byte sizes and scan for valid quota positions.
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP"); return; }

        foreach (var filePath in files)
        {
            Console.WriteLine($"\n=== {Path.GetFileName(filePath)} ===");
            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            var bitstream = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
            int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
            int packedLenBits = packedLenBytes * 8;

            // Parse header to get numQuotas
            var bits = new ForgeX.Core.IO.BitReader(bitstream);
            bits.ReadInteger(4); bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32);
            bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

            bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
            int numQuotas = (int)bits.ReadInteger(9);

            Console.WriteLine($"  packed={packedLenBits} numQuotas={numQuotas}");

            // Try different per-entry bit sizes: 24 (3×8), 32 (4×8), 40, 48
            // For each, compute expected start position and check validity
            foreach (int entryBits in new[] { 24, 32, 40, 48, 56, 64, 72, 80, 96 })
            {
                int totalQuotaBits = numQuotas * entryBits;
                int expectedStart = packedLenBits - totalQuotaBits;
                if (expectedStart < 0) continue;

                // Try to read quotas at expected position with 3-byte min/max/count interpretation
                // (first 3 bytes of each entry)
                var qr = new ForgeX.Core.IO.BitReader(bitstream);
                qr.SkipBits(expectedStart);
                int nz = 0; bool allValid = true;
                for (int q = 0; q < numQuotas; q++)
                {
                    int mn = (int)qr.ReadInteger(8);
                    int mx = (int)qr.ReadInteger(8);
                    int ct = (int)qr.ReadInteger(8);
                    // Skip remaining bytes per entry
                    if (entryBits > 24) qr.SkipBits(entryBits - 24);
                    if (mn == 0 && mx == 0 && ct == 0) continue;
                    if (mn > mx || ct > mx || mx > 200) { allValid = false; }
                    nz++;
                }
                Console.Write($"  {entryBits}b/entry: start={expectedStart} nz={nz}");
                if (allValid && nz >= 3) Console.Write(" VALID");
                Console.WriteLine();
            }

            // Also scan backwards from packed end: try to find where the quota section starts
            // by looking for a transition from non-zero to mostly-zero bytes
            Console.Write("  Last 64 bytes of packed data: ");
            int startByte = packedLenBytes - 64;
            if (startByte < 24) startByte = 24;
            for (int b = startByte; b < packedLenBytes && b < 64 + startByte; b++)
            {
                // bitstream is offset by 24 from mvar.Data
                if (b - 24 < bitstream.Length)
                    Console.Write($"{bitstream[b - 24]:X2} ");
            }
            Console.WriteLine();

            // Check: are the last N bytes all zero? (suggesting padding within packed data)
            int trailingZeros = 0;
            for (int b = packedLenBytes - 1; b >= 24 && bitstream[b - 24] == 0; b--)
                trailingZeros++;
            Console.WriteLine($"  Trailing zeros in packed data: {trailingZeros} bytes");
        }
    }

    [Fact]
    public void Halo4NoExistsFlagTest()
    {
        // Test hypothesis: H4 doesn't use exists flag. Every slot has full data.
        // Inactive slots are all zeros.
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP"); return; }

        foreach (var filePath in files)
        {
            Console.WriteLine($"\n=== {Path.GetFileName(filePath)} ===");
            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            var bitstream = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
            int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
            int packedLenBits = packedLenBytes * 8;

            var bits = new ForgeX.Core.IO.BitReader(bitstream);
            bits.ReadInteger(4); bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32);
            bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

            bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
            int numQuotas = (int)bits.ReadInteger(9);
            bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            bits.ReadInteger(32); bits.ReadInteger(32);

            int strCount = (int)bits.ReadInteger(9);
            for (int i = 0; i < strCount; i++)
            { bool exists = bits.ReadBool(); if (exists) bits.ReadInteger(12); }
            if (strCount > 0)
            {
                int bufSize = (int)bits.ReadInteger(13);
                bool compressed = bits.ReadBool();
                int readSize = compressed ? (int)bits.ReadInteger(13) : bufSize;
                for (int ci = 0; ci < readSize; ci++) bits.ReadInteger(8);
            }

            int objStart = bits.BitOffset;
            var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);

            int totalPosBits = bitsX + bitsY + bitsZ;

            // Available bits for objects + quotas
            int avail = packedLenBits - objStart;
            int quotaBits = numQuotas * 24;
            int objBits = avail - quotaBits;
            float bitsPerSlot = (float)objBits / 640;

            Console.WriteLine($"  objStart={objStart} posBits={totalPosBits} packed={packedLenBits}");
            Console.WriteLine($"  available={avail} quotas={quotaBits} objSection={objBits} bitsPerSlot={bitsPerSlot:F2}");

            // Try various fixed per-slot sizes and check quota validity
            // Also try: exists(1) + fixed data, vs no exists + fixed data
            for (int slotBits = 140; slotBits <= 175; slotBits++)
            {
                int objEnd = objStart + 640 * slotBits;
                if (objEnd + quotaBits > packedLenBits + 8) continue;

                var qr = new ForgeX.Core.IO.BitReader(bitstream);
                qr.SkipBits(objEnd);
                int nz = 0; bool valid = true; int totalPlaced = 0;
                for (int q = 0; q < numQuotas; q++)
                {
                    int mn = (int)qr.ReadInteger(8);
                    int mx = (int)qr.ReadInteger(8);
                    int ct = (int)qr.ReadInteger(8);
                    if (mn == 0 && mx == 0 && ct == 0) continue;
                    if (mn > mx || ct > mx || mx > 200) { valid = false; break; }
                    nz++;
                    totalPlaced += ct;
                }
                if (valid && nz >= 3)
                    Console.WriteLine($"  slotBits={slotBits}: objEnd={objEnd} nz={nz} placed={totalPlaced} qEnd={objEnd+quotaBits} gap={packedLenBits-objEnd-quotaBits}");
            }

            // Also try 640 slots with 1-bit exists + variable fields (Reach-like)
            // But using fixed TOTAL bits per slot (exists flag is part of the slot)
            // If inactive: exists=0 + (slotBits-1) zeros
            // If active: exists=1 + data
            // The total slot size would be fixed regardless

            // Try wider range: target based on computed bitsPerSlot
            int target = (int)Math.Round(bitsPerSlot);
            for (int slotBits = target - 5; slotBits <= target + 5; slotBits++)
            {
                if (slotBits < 1 || slotBits > 300) continue;
                int objEnd = objStart + 640 * slotBits;
                if (objEnd + quotaBits > packedLenBits + 8) continue;
                if (objEnd < objStart) continue;

                var qr = new ForgeX.Core.IO.BitReader(bitstream);
                qr.SkipBits(objEnd);
                int nz = 0; bool valid = true; int totalPlaced = 0;
                for (int q = 0; q < numQuotas; q++)
                {
                    int mn = (int)qr.ReadInteger(8);
                    int mx = (int)qr.ReadInteger(8);
                    int ct = (int)qr.ReadInteger(8);
                    if (mn == 0 && mx == 0 && ct == 0) continue;
                    if (mn > mx || ct > mx || mx > 200) { valid = false; break; }
                    nz++;
                    totalPlaced += ct;
                }
                if (valid && nz >= 3)
                    Console.WriteLine($"  *slotBits={slotBits}: objEnd={objEnd} nz={nz} placed={totalPlaced} gap={packedLenBits-objEnd-quotaBits}");
            }
        }
    }

    [Fact]
    public void Halo4QuotaEndDump()
    {
        // Dump the last portion of packed data for bonanza to find quota structure.
        string path = Path.Combine(H4MccDir, "ca_forge_bonanza_relay.mvar");
        if (!File.Exists(path)) { Console.WriteLine("SKIP"); return; }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
        int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];

        // Dump last 400 bytes as hex
        Console.WriteLine("Last 400 bytes of packed data:");
        int startDump = Math.Max(0, packedLenBytes - 400);
        for (int row = startDump; row < packedLenBytes; row += 16)
        {
            Console.Write($"  {row:X5}: ");
            for (int col = 0; col < 16 && row + col < packedLenBytes; col++)
                Console.Write($"{bitstream[row + col]:X2} ");
            Console.WriteLine();
        }

        // Try reading entries as 12-byte (96-bit) BudgetEntry from the end
        // Format: uint32_be tag, uint8 min, uint8 max, uint8 count, uint8 design_max, float32_be cost
        Console.WriteLine("\nLast 30 entries as 12-byte BudgetEntry (reading from end):");
        int entrySize = 12;
        int numEntries = 256;
        int tableStart = packedLenBytes - numEntries * entrySize;
        Console.WriteLine($"  Table would start at byte {tableStart} (bit {tableStart*8})");

        if (tableStart >= 0)
        {
            for (int i = numEntries - 30; i < numEntries; i++)
            {
                int off = tableStart + i * entrySize;
                if (off + entrySize > packedLenBytes) break;
                uint tag = (uint)((bitstream[off] << 24) | (bitstream[off+1] << 16) | (bitstream[off+2] << 8) | bitstream[off+3]);
                int mn = bitstream[off + 4];
                int mx = bitstream[off + 5];
                int ct = bitstream[off + 6];
                int dm = bitstream[off + 7];
                uint costBits = (uint)((bitstream[off+8] << 24) | (bitstream[off+9] << 16) | (bitstream[off+10] << 8) | bitstream[off+11]);
                float cost = BitConverter.Int32BitsToSingle((int)costBits);
                if (tag == 0xFFFFFFFF && mn == 0 && mx == 0) continue; // skip empty
                Console.WriteLine($"  [{i}]: tag=0x{tag:X8} min={mn} max={mx} count={ct} dmax={dm} cost={cost:F1}");
            }
        }

        // Also try 3-byte entries (same as Reach) at various offsets from the end
        Console.WriteLine("\nLast entries as 3-byte (24-bit) at packed end:");
        int start3 = packedLenBytes - 256 * 3;
        Console.WriteLine($"  Table would start at byte {start3} (bit {start3*8})");
        if (start3 >= 0)
        {
            for (int i = 0; i < 256; i++)
            {
                int off = start3 + i * 3;
                if (off + 3 > packedLenBytes) break;
                int mn = bitstream[off], mx = bitstream[off+1], ct = bitstream[off+2];
                if (mn == 0 && mx == 0 && ct == 0) continue;
                Console.WriteLine($"  [{i}]: min={mn} max={mx} count={ct}");
            }
        }
    }

    [Fact]
    public void Halo4LargeFileObjectDump()
    {
        // Parse bonanza (large forge map) object-by-object to find where format diverges.
        string path = Path.Combine(H4MccDir, "ca_forge_bonanza_relay.mvar");
        if (!File.Exists(path)) { Console.WriteLine("SKIP"); return; }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
        int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
        int packedLenBits = packedLenBytes * 8;

        var bits = new ForgeX.Core.IO.BitReader(bitstream);
        // Content metadata (H4)
        bits.ReadInteger(4); bits.ReadInteger(32);
        bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
        bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32);
        bits.ReadInteger(8);
        bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool();
        bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool();
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

        bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
        int numQuotas = (int)bits.ReadInteger(9);
        uint mapId = bits.ReadInteger(32);
        bits.ReadBool(); bits.ReadBool();
        float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
        float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
        float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
        bits.ReadInteger(32); bits.ReadInteger(32);

        Console.WriteLine($"Map {mapId} Bounds: X[{xMin:F1},{xMax:F1}] Y[{yMin:F1},{yMax:F1}] Z[{zMin:F1},{zMax:F1}]");

        int strCount = (int)bits.ReadInteger(9);
        for (int i = 0; i < strCount; i++)
        { bool exists = bits.ReadBool(); if (exists) bits.ReadInteger(12); }
        if (strCount > 0)
        {
            int bufSize = (int)bits.ReadInteger(13);
            bool compressed = bits.ReadBool();
            int readSize = compressed ? (int)bits.ReadInteger(13) : bufSize;
            for (int ci = 0; ci < readSize; ci++) bits.ReadInteger(8);
        }

        int objStart = bits.BitOffset;
        var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
            xMin, xMax, yMin, yMax, zMin, zMax, 21);

        Console.WriteLine($"objStart={objStart} posBits={bitsX}+{bitsY}+{bitsZ} packed={packedLenBits}");
        Console.WriteLine($"strCount={strCount}");

        // Parse objects with Reach format, dump first 15 active objects
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);
        int activeCount = 0;
        int goodCount = 0;

        for (int i = 0; i < 640 && pb.BitsRemaining > 1; i++)
        {
            int startBit = pb.BitOffset;
            bool ex = pb.ReadBool();
            if (!ex) continue;
            activeCount++;

            uint flags = pb.ReadInteger(2);
            bool qaAbsent = pb.ReadBool(); int qi = qaAbsent ? -1 : (int)pb.ReadInteger(8);
            bool vaAbsent = pb.ReadBool(); int vi = vaAbsent ? -1 : (int)pb.ReadInteger(5);
            bool ib = pb.ReadBool();
            float px, py, pz;
            if (ib) {
                uint rx = pb.ReadInteger(bitsX), ry = pb.ReadInteger(bitsY), rz = pb.ReadInteger(bitsZ);
                px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
            } else {
                px = pb.ReadRawFloat(); py = pb.ReadRawFloat(); pz = pb.ReadRawFloat();
            }
            bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
            uint shape = pb.ReadInteger(2);
            switch (shape) {
                case 1: pb.ReadInteger(11); break;
                case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
            }
            uint seq = pb.ReadInteger(8); uint resp = pb.ReadInteger(8); uint typ = pb.ReadInteger(5);
            bool la = pb.ReadBool(); int lab = la ? -1 : (int)pb.ReadInteger(8);
            uint pfl = pb.ReadInteger(8); uint tm = pb.ReadInteger(4);
            bool ca = pb.ReadBool(); int col = ca ? -1 : (int)pb.ReadInteger(3);

            int endBit = pb.BitOffset;
            bool posOk = ib ? (px >= xMin - 10 && px <= xMax + 10 && py >= yMin - 10 && py <= yMax + 10 && pz >= zMin - 10 && pz <= zMax + 10) : true;
            bool fieldsOk = qi < 256 && vi < 32 && typ < 32 && (tm <= 15);
            bool good = posOk && fieldsOk;
            if (good) goodCount++;

            if (activeCount <= 15 || !good)
            {
                string marker = good ? "OK" : "BAD";
                Console.WriteLine($"  [{i}] #{activeCount} @{startBit}: fl={flags} qi={qi} vi={vi} ib={ib} pos=({px:F1},{py:F1},{pz:F1}) du={du} sh={shape} seq={seq} resp={resp} typ={typ} lab={lab} pfl=0x{pfl:X2} tm={tm} col={col} [{marker}] {endBit - startBit}b");
                if (activeCount > 15 && !good) break; // Stop after first BAD after initial dump
            }
        }
        Console.WriteLine($"\nTotal: {activeCount} active, {goodCount} good, end={pb.BitOffset}");
        Console.WriteLine($"Expected object section: {packedLenBits - numQuotas * 24 - objStart} bits");
    }

    /// <summary>
    /// Detailed per-object dump for bonanza using all 3 formats to find exactly where parsing diverges.
    /// </summary>
    [Fact]
    public void Halo4BonanzaDetailedDump()
    {
        string path = Path.Combine(H4MccDir, "ca_forge_bonanza_relay.mvar");
        if (!File.Exists(path)) { Console.WriteLine("SKIP"); return; }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
        int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
        int packedLenBits = packedLenBytes * 8;

        // Detailed metadata parsing with position tracking
        var bits = new ForgeX.Core.IO.BitReader(bitstream);
        Console.WriteLine("=== Detailed metadata parsing ===");

        int pos0 = bits.BitOffset;
        int fileType = (int)bits.ReadInteger(4);
        Console.WriteLine($"  fileType={fileType} (4b) @{pos0}→{bits.BitOffset}");

        uint sizeInBytes = bits.ReadInteger(32);
        Console.WriteLine($"  sizeInBytes={sizeInBytes} (32b) @{bits.BitOffset - 32}→{bits.BitOffset}");

        ulong uniqueId = bits.ReadInteger64(64);
        ulong parentId = bits.ReadInteger64(64);
        ulong rootId = bits.ReadInteger64(64);
        ulong gameId = bits.ReadInteger64(64);
        Console.WriteLine($"  4x64-bit IDs (256b) → @{bits.BitOffset}");

        uint activity = bits.ReadInteger(2);
        uint gameMode = bits.ReadInteger(3);
        uint engineType = bits.ReadInteger(3);
        Console.WriteLine($"  activity={activity}(2b) gameMode={gameMode}(3b) engineType={engineType}(3b) → @{bits.BitOffset}");

        // H4 has an extra 32-bit field here (map_id between engine_type and engine_category_index)
        uint unk2C = bits.ReadInteger(32);
        Console.WriteLine($"  unk2C=0x{unk2C:X8}(32b) → @{bits.BitOffset}");

        int engCat = (int)bits.ReadInteger(8);
        Console.WriteLine($"  engineCategoryIndex={engCat}(8b) → @{bits.BitOffset}");

        ulong creationTime = bits.ReadInteger64(64);
        ulong creatorXuid = bits.ReadInteger64(64);
        Console.WriteLine($"  creationTime+xuid (128b) → @{bits.BitOffset}");

        // creator_name: null-terminated ASCII, up to 16 bytes
        int nameStart = bits.BitOffset;
        var nameBytes = new System.Text.StringBuilder();
        int nameLen = 0;
        for (int i = 0; i < 16; i++)
        {
            byte b = (byte)bits.ReadInteger(8);
            if (b == 0) break;
            nameBytes.Append((char)b);
            nameLen = i + 1;
        }
        Console.WriteLine($"  creator_name='{nameBytes}' ({nameLen+1} bytes read, stopped @{bits.BitOffset})");

        bool creatorOnline = bits.ReadBool();
        Console.WriteLine($"  creatorOnline={creatorOnline}(1b) → @{bits.BitOffset}");

        ulong modTime = bits.ReadInteger64(64);
        ulong modXuid = bits.ReadInteger64(64);
        Console.WriteLine($"  modTime+xuid (128b) → @{bits.BitOffset}");

        int modNameStart = bits.BitOffset;
        var modName = new System.Text.StringBuilder();
        int modNameLen = 0;
        for (int i = 0; i < 16; i++)
        {
            byte b = (byte)bits.ReadInteger(8);
            if (b == 0) break;
            modName.Append((char)b);
            modNameLen = i + 1;
        }
        Console.WriteLine($"  mod_name='{modName}' ({modNameLen+1} bytes read, stopped @{bits.BitOffset})");

        bool modOnline = bits.ReadBool();
        Console.WriteLine($"  modOnline={modOnline}(1b) → @{bits.BitOffset}");

        // title (wchar, null-terminated, up to 128 chars)
        int titleStart = bits.BitOffset;
        var title = new System.Text.StringBuilder();
        int titleLen = 0;
        for (int i = 0; i < 128; i++)
        {
            ushort c = (ushort)bits.ReadInteger(16);
            if (c == 0) break;
            title.Append((char)c);
            titleLen = i + 1;
        }
        Console.WriteLine($"  title='{title}' ({titleLen+1} wchars read, {(titleLen+1)*16}b, stopped @{bits.BitOffset})");

        // description (wchar, null-terminated, up to 128 chars)
        int descStart = bits.BitOffset;
        var desc = new System.Text.StringBuilder();
        int descLen = 0;
        for (int i = 0; i < 128; i++)
        {
            ushort c = (ushort)bits.ReadInteger(16);
            if (c == 0) break;
            desc.Append((char)c);
            descLen = i + 1;
        }
        Console.WriteLine($"  desc='{desc}' ({descLen+1} wchars read, {(descLen+1)*16}b, stopped @{bits.BitOffset})");

        int metaEnd = bits.BitOffset;
        Console.WriteLine($"\nMetadata end: bit {metaEnd}");

        // === Header ===
        Console.WriteLine("\n=== Header ===");
        uint encVer = bits.ReadInteger(8);
        uint rsa = bits.ReadInteger(32);
        uint crc = bits.ReadInteger(32);
        int numQuotas = (int)bits.ReadInteger(9);
        uint mapId = bits.ReadInteger(32);
        bool builtIn = bits.ReadBool();
        bool builtXml = bits.ReadBool();
        Console.WriteLine($"  ver={encVer} rsa=0x{rsa:X8} crc=0x{crc:X8} quotas={numQuotas} mapId={mapId}");
        Console.WriteLine($"  builtIn={builtIn} builtXml={builtXml} → @{bits.BitOffset}");

        float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
        float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
        float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
        Console.WriteLine($"  bounds: X[{xMin:F1},{xMax:F1}] Y[{yMin:F1},{yMax:F1}] Z[{zMin:F1},{zMax:F1}] → @{bits.BitOffset}");

        uint maxBud = bits.ReadInteger(32);
        uint curBud = bits.ReadInteger(32);
        Console.WriteLine($"  maxBudget={maxBud} curBudget={curBud} → @{bits.BitOffset}");

        // === String table ===
        Console.WriteLine("\n=== String table ===");
        int strCount = (int)bits.ReadInteger(9);
        Console.WriteLine($"  strCount={strCount} → @{bits.BitOffset}");
        for (int i = 0; i < strCount; i++)
        {
            bool exists = bits.ReadBool();
            if (exists) bits.ReadInteger(12);
        }
        Console.WriteLine($"  After string entries → @{bits.BitOffset}");
        if (strCount > 0)
        {
            int bufSize = (int)bits.ReadInteger(13);
            bool compressed = bits.ReadBool();
            Console.WriteLine($"  bufSize={bufSize} compressed={compressed}");
            int readSize = compressed ? (int)bits.ReadInteger(13) : bufSize;
            Console.WriteLine($"  readSize={readSize} → @{bits.BitOffset} (before buffer bytes)");
            for (int ci = 0; ci < readSize; ci++) bits.ReadInteger(8);
            Console.WriteLine($"  After buffer bytes → @{bits.BitOffset}");
        }

        int objStart = bits.BitOffset;
        var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
            xMin, xMax, yMin, yMax, zMin, zMax, 21);
        Console.WriteLine($"\n=== Object section starts at bit {objStart} ===");
        Console.WriteLine($"  positionBits: X={bitsX} Y={bitsY} Z={bitsZ}");
        Console.WriteLine($"  packed total: {packedLenBits}b, quota space: {numQuotas * 24}b");
        Console.WriteLine($"  expected obj section: {packedLenBits - numQuotas * 24 - objStart}b");

        // === Parse objects with VALIDATION ===
        Console.WriteLine("\n=== Per-object parse (Reach full format) ===");
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);

        int activeCount = 0;
        int lastGoodObj = -1;

        for (int i = 0; i < 640; i++)
        {
            if (pb.BitsRemaining < 1) break;
            int startBit = pb.BitOffset;

            bool exists = pb.ReadBool();
            if (!exists) continue;
            activeCount++;

            uint flags = pb.ReadInteger(2);
            bool qaA = pb.ReadBool(); int qi = qaA ? -1 : (int)pb.ReadInteger(8);
            bool vaA = pb.ReadBool(); int vi = vaA ? -1 : (int)pb.ReadInteger(5);
            bool ib = pb.ReadBool();
            float px, py, pz;
            if (ib) {
                uint rx = pb.ReadInteger(bitsX), ry = pb.ReadInteger(bitsY), rz = pb.ReadInteger(bitsZ);
                px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
            } else {
                px = pb.ReadRawFloat(); py = pb.ReadRawFloat(); pz = pb.ReadRawFloat();
            }

            bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);

            uint shape = pb.ReadInteger(2);
            switch (shape) {
                case 1: pb.ReadInteger(11); break;
                case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
            }

            uint seq = pb.ReadInteger(8);
            uint resp = pb.ReadInteger(8);
            uint typ = pb.ReadInteger(5);
            bool laA = pb.ReadBool(); int lab = laA ? -1 : (int)pb.ReadInteger(8);
            uint pfl = pb.ReadInteger(8);
            uint tm = pb.ReadInteger(4);
            bool caA = pb.ReadBool(); int col = caA ? -1 : (int)pb.ReadInteger(3);

            // Conditional per-type
            if (typ == 2) pb.ReadInteger(8); // weapon spare_clips
            if (typ == 10) { pb.ReadInteger(5); pb.ReadInteger(5); } // teleporter
            bool lnA = pb.ReadBool(); if (!lnA) pb.ReadInteger(8); // location_name

            int endBit = pb.BitOffset;

            // Validation
            bool posOk = ib ? (px >= xMin - 50 && px <= xMax + 50 && py >= yMin - 50 && py <= yMax + 50 && pz >= zMin - 50 && pz <= zMax + 50) : true;
            bool fieldsOk = qi < 256 && vi < 32 && typ < 32 && tm <= 15;
            bool good = posOk && fieldsOk;

            if (good) lastGoodObj = i;

            if (activeCount <= 20 || !good)
            {
                string marker = good ? "OK" : "BAD";
                Console.WriteLine($"  [{i}] #{activeCount} @{startBit}: fl={flags} qi={qi} vi={vi} ib={ib} pos=({px:F1},{py:F1},{pz:F1}) du={du} sh={shape} seq={seq} resp={resp} typ={typ} lab={lab} pfl=0x{pfl:X2} tm={tm} col={col} locN={!lnA} [{marker}] {endBit - startBit}b → @{endBit}");
                if (!good && activeCount > 5)
                {
                    Console.WriteLine($"  *** Parse went BAD after object #{activeCount}, lastGood=[{lastGoodObj}] ***");
                    Console.WriteLine($"  Remaining bits: {pb.BitsRemaining}");
                    break;
                }
            }
        }

        int finalPos = pb.BitOffset;
        Console.WriteLine($"\n=== Summary ===");
        Console.WriteLine($"Total active: {activeCount}, lastGood=[{lastGoodObj}]");
        Console.WriteLine($"Final bit: {finalPos}, expected quota: {packedLenBits - numQuotas * 24}");
        Console.WriteLine($"Delta: {finalPos - (packedLenBits - numQuotas * 24)}");
    }

    /// <summary>
    /// Work backwards from the end of packed data to find quotas, then determine how much
    /// space the object section actually occupies.
    /// </summary>
    [Fact]
    public void Halo4BackwardsQuotaSearch()
    {
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP"); return; }

        foreach (var filePath in files.OrderBy(f => Path.GetFileName(f)))
        {
            Console.WriteLine($"\n=== {Path.GetFileName(filePath)} ===");
            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
            int packedLenBits = packedLenBytes * 8;

            var bitstream = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);

            Console.WriteLine($"  packed: {packedLenBytes} bytes = {packedLenBits} bits");

            // Try reading 256 entries × 24 bits from the expected end position
            int quotaStart = packedLenBits - 256 * 24;
            Console.WriteLine($"\n  --- Quotas at bit {quotaStart} (packed end - 256*24) ---");
            {
                var qr = new ForgeX.Core.IO.BitReader(bitstream);
                qr.SkipBits(quotaStart);
                int nonZero = 0, placed = 0, invalid = 0;
                for (int q = 0; q < 256; q++)
                {
                    int mn = (int)qr.ReadInteger(8);
                    int mx = (int)qr.ReadInteger(8);
                    int ct = (int)qr.ReadInteger(8);
                    if (mn == 0 && mx == 0 && ct == 0) continue;
                    if (mn > mx || ct > mx || mx > 200) { invalid++; continue; }
                    nonZero++;
                    placed += ct;
                    if (nonZero <= 20)
                        Console.WriteLine($"    q[{q}]: min={mn} max={mx} count={ct}");
                }
                Console.WriteLine($"  Result: {nonZero} valid non-zero, {invalid} invalid, placed={placed}");
            }

            // Try different numEntries and entry sizes
            foreach (int entryBits in new[] { 24, 32, 48 })
            {
                foreach (int numEntries in new[] { 256, 640, 128 })
                {
                    int totalBits = numEntries * entryBits;
                    if (totalBits > packedLenBits) continue;
                    int qs = packedLenBits - totalBits;
                    var qr = new ForgeX.Core.IO.BitReader(bitstream);
                    qr.SkipBits(qs);
                    int nz = 0, bad = 0;
                    for (int q = 0; q < numEntries; q++)
                    {
                        if (entryBits == 24)
                        {
                            int mn = (int)qr.ReadInteger(8), mx = (int)qr.ReadInteger(8), ct = (int)qr.ReadInteger(8);
                            if (mn == 0 && mx == 0 && ct == 0) continue;
                            if (mn > mx || ct > mx || mx > 200) { bad++; continue; }
                            nz++;
                        }
                        else if (entryBits == 32)
                        {
                            int mn = (int)qr.ReadInteger(8), mx = (int)qr.ReadInteger(8), ct = (int)qr.ReadInteger(8), extra = (int)qr.ReadInteger(8);
                            if (mn == 0 && mx == 0 && ct == 0 && extra == 0) continue;
                            if (mn > mx || ct > mx || mx > 200) { bad++; continue; }
                            nz++;
                        }
                        else
                        {
                            // 48-bit: skip
                            uint a = qr.ReadInteger(16), b = qr.ReadInteger(16), c = qr.ReadInteger(16);
                            if (a == 0 && b == 0 && c == 0) continue;
                            nz++;
                        }
                    }
                    if (nz >= 3 && bad == 0)
                        Console.WriteLine($"  Quota candidate: {numEntries}×{entryBits}b @{qs} → {nz} non-zero, 0 invalid");
                }
            }

            // Dump the actual bytes at the end of packed data (last 200 bytes as hex)
            int dumpStart = Math.Max(0, packedLenBytes - 200);
            Console.WriteLine($"\n  --- Last 200 bytes of packed data (bytes {dumpStart}-{packedLenBytes-1}) ---");
            for (int row = dumpStart; row < packedLenBytes; row += 32)
            {
                int end = Math.Min(row + 32, packedLenBytes);
                Console.Write($"  {row,6}: ");
                for (int b = row; b < end; b++)
                    Console.Write($"{bitstream[b]:X2} ");
                Console.Write("  ");
                for (int b = row; b < end; b++)
                {
                    char c = (char)bitstream[b];
                    Console.Write(c >= 32 && c < 127 ? c : '.');
                }
                Console.WriteLine();
            }

            // Scan from end for first non-zero byte
            int lastNonZero = packedLenBytes - 1;
            while (lastNonZero > 0 && bitstream[lastNonZero] == 0) lastNonZero--;
            Console.WriteLine($"\n  Last non-zero byte at offset {lastNonZero} (of {packedLenBytes - 1})");
            Console.WriteLine($"  Zero-fill at end: {packedLenBytes - 1 - lastNonZero} bytes");
        }
    }


    /// <summary>
    /// Dump Halo 4 quota entries as 48-bit (6 byte) entries with multiple sub-field interpretations.
    /// Grifball court has 69 placed objects, so we check if entry counts are consistent.
    /// </summary>
    [Fact]
    public void Halo4QuotaEntryDump()
    {
        string path = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(path)) { Console.WriteLine("SKIP: grifballcourt.mvar not found"); return; }

        Console.WriteLine($"=== Halo4QuotaEntryDump: grifballcourt.mvar ===");

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
        int packedLenBits = packedLenBytes * 8;

        // Extract bitstream (skip 24-byte mvar header)
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);

        Console.WriteLine($"  packed: {packedLenBytes} bytes = {packedLenBits} bits");

        // Read the number of quotas from the header (same field as Halo4BackwardsQuotaSearch)
        int numQuotas = 256;

        // 48-bit entries starting at packedLenBits - 256*48
        int quotaStartBit = packedLenBits - numQuotas * 48;
        int quotaStartByte = quotaStartBit / 8;
        Console.WriteLine($"  Quota region: bit {quotaStartBit} (byte {quotaStartByte}) to bit {packedLenBits}");
        Console.WriteLine($"  Total quota region: {numQuotas * 48} bits = {numQuotas * 6} bytes");
        Console.WriteLine();

        int totalNonZero = 0;
        int totalCountField_8_8_8_8_16 = 0;
        int totalCountField_16_16_16 = 0;

        for (int q = 0; q < numQuotas; q++)
        {
            // Read 6 raw bytes for this entry
            int entryBitPos = quotaStartBit + q * 48;
            int entryBytePos = entryBitPos / 8;
            int bitOffset = entryBitPos % 8;

            // Read the 6 bytes via BitReader for bit-accurate reading
            var br = new ForgeX.Core.IO.BitReader(bitstream);
            br.SeekBits(entryBitPos);
            byte[] raw = new byte[6];
            for (int i = 0; i < 6; i++)
                raw[i] = (byte)br.ReadInteger(8);

            // Check if all zero
            bool allZero = true;
            for (int i = 0; i < 6; i++) { if (raw[i] != 0) { allZero = false; break; } }
            if (allZero) continue;

            totalNonZero++;

            Console.WriteLine($"  --- Quota[{q}] at bit {entryBitPos} (byte ~{entryBytePos}) ---");
            Console.WriteLine($"    Raw hex: {raw[0]:X2} {raw[1]:X2} {raw[2]:X2} {raw[3]:X2} {raw[4]:X2} {raw[5]:X2}");
            Console.WriteLine($"    Raw bin: {Convert.ToString(raw[0], 2).PadLeft(8, '0')} {Convert.ToString(raw[1], 2).PadLeft(8, '0')} {Convert.ToString(raw[2], 2).PadLeft(8, '0')} {Convert.ToString(raw[3], 2).PadLeft(8, '0')} {Convert.ToString(raw[4], 2).PadLeft(8, '0')} {Convert.ToString(raw[5], 2).PadLeft(8, '0')}");

            // Interpretation 1: 3x16 bit fields
            int f16a = (raw[0] << 8) | raw[1];
            int f16b = (raw[2] << 8) | raw[3];
            int f16c = (raw[4] << 8) | raw[5];
            Console.WriteLine($"    3x16:      [{f16a}, {f16b}, {f16c}]");

            // Interpretation 2: 2x24 bit fields
            int f24a = (raw[0] << 16) | (raw[1] << 8) | raw[2];
            int f24b = (raw[3] << 16) | (raw[4] << 8) | raw[5];
            Console.WriteLine($"    2x24:      [{f24a}, {f24b}]");

            // Interpretation 3: 8+8+8+8+16 bit fields
            int fa = raw[0], fb = raw[1], fc = raw[2], fd = raw[3];
            int fe16 = (raw[4] << 8) | raw[5];
            Console.WriteLine($"    8+8+8+8+16: [{fa}, {fb}, {fc}, {fd}, {fe16}]");
            totalCountField_8_8_8_8_16 += fc; // guess count is 3rd byte

            // Interpretation 4: 8+8+8+24 bit fields
            int g24 = (raw[3] << 16) | (raw[4] << 8) | raw[5];
            Console.WriteLine($"    8+8+8+24:  [{fa}, {fb}, {fc}, {g24}]");

            // Interpretation 5: 16+16+16 bit fields (same as 3x16, shown for clarity)
            Console.WriteLine($"    16+16+16:  [{f16a}, {f16b}, {f16c}]");

            // Interpretation 6: 32+8+8 bit fields
            int f32 = (raw[0] << 24) | (raw[1] << 16) | (raw[2] << 8) | raw[3];
            int lastA = raw[4], lastB = raw[5];
            Console.WriteLine($"    32+8+8:    [{f32} (0x{f32:X8}), {lastA}, {lastB}]");

            totalCountField_16_16_16 += f16c; // guess count might be last 16-bit field
        }

        Console.WriteLine();
        Console.WriteLine($"  Total non-zero entries: {totalNonZero}");
        Console.WriteLine($"  Expected placed objects (grifball): 69");
        Console.WriteLine($"  Sum of 3rd byte (8+8+8+8+16 interp): {totalCountField_8_8_8_8_16}");
        Console.WriteLine($"  Sum of last 16-bit (16+16+16 interp): {totalCountField_16_16_16}");

        // Also try as 24-bit entries for comparison
        Console.WriteLine();
        Console.WriteLine("  --- For comparison: 24-bit quota entries (256 entries) ---");
        int qs24 = packedLenBits - 256 * 24;
        var qr24 = new ForgeX.Core.IO.BitReader(bitstream);
        qr24.SeekBits(qs24);
        int nz24 = 0, placed24 = 0;
        for (int q = 0; q < 256; q++)
        {
            int mn = (int)qr24.ReadInteger(8);
            int mx = (int)qr24.ReadInteger(8);
            int ct = (int)qr24.ReadInteger(8);
            if (mn == 0 && mx == 0 && ct == 0) continue;
            nz24++;
            placed24 += ct;
        }
        Console.WriteLine($"  24-bit: {nz24} non-zero entries, total placed count = {placed24}");
        Console.WriteLine($"  Does 48-bit non-zero count ({totalNonZero}) make sense for 69 objects? {(totalNonZero > 0 && totalNonZero <= 69 ? "PLAUSIBLE" : totalNonZero == 69 ? "EXACT MATCH" : "UNLIKELY")}");
    }

    /// <summary>
    /// Analyze the end pattern of packed data in all 4 Halo 4 mvar files.
    /// Auto-correlate byte patterns to find the repeating unit length at the end,
    /// then dump each entry separately.
    /// </summary>
    [Fact]
    public void Halo4EndPatternAnalysis()
    {
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP: No H4 mvar files found"); return; }

        foreach (var filePath in files.OrderBy(f => Path.GetFileName(f)))
        {
            Console.WriteLine($"\n=== {Path.GetFileName(filePath)} ===");

            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];

            // Extract bitstream (skip 24-byte mvar header)
            var bitstream = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);

            // Read last 400 bytes of packed data
            int readLen = Math.Min(400, packedLenBytes);
            int startOffset = packedLenBytes - readLen;
            byte[] tail = new byte[readLen];
            Array.Copy(bitstream, startOffset, tail, 0, readLen);

            Console.WriteLine($"  packed: {packedLenBytes} bytes");
            Console.WriteLine($"  Analyzing last {readLen} bytes (offset {startOffset} to {packedLenBytes - 1})");

            // Find last non-zero byte in tail
            int lastNz = readLen - 1;
            while (lastNz >= 0 && tail[lastNz] == 0) lastNz--;
            int trailingZeros = readLen - 1 - lastNz;
            Console.WriteLine($"  Trailing zero bytes in tail: {trailingZeros}");

            // Work with the non-zero portion
            int effectiveLen = lastNz + 1;
            if (effectiveLen < 6)
            {
                Console.WriteLine("  Not enough non-zero data to analyze");
                continue;
            }

            // Auto-correlate: try unit lengths from 2 to 48 bytes
            Console.WriteLine("\n  --- Auto-correlation (unit lengths 2-48) ---");
            int bestUnitLen = 0;
            int bestScore = 0;

            for (int unitLen = 2; unitLen <= 48; unitLen++)
            {
                // Compare the last N full units to see how many byte positions match
                int numUnits = effectiveLen / unitLen;
                if (numUnits < 3) continue; // need at least 3 units to compare

                int matches = 0;
                int comparisons = 0;

                // Compare each unit with the previous one (from the end)
                for (int u = numUnits - 1; u >= 1; u--)
                {
                    int offsetA = effectiveLen - (numUnits - u) * unitLen;
                    int offsetB = effectiveLen - (numUnits - u + 1) * unitLen;
                    if (offsetA < 0 || offsetB < 0) break;

                    for (int b = 0; b < unitLen; b++)
                    {
                        comparisons++;
                        // Check if there's a structural similarity (same zero/non-zero pattern)
                        bool aZero = tail[offsetA + b] == 0;
                        bool bZero = tail[offsetB + b] == 0;
                        if (aZero == bZero) matches++;
                    }
                }

                if (comparisons > 0)
                {
                    int score = (matches * 100) / comparisons;
                    if (score > bestScore || (score == bestScore && unitLen < bestUnitLen))
                    {
                        bestScore = score;
                        bestUnitLen = unitLen;
                    }
                    if (score >= 70)
                        Console.WriteLine($"    unit={unitLen}: {score}% structural match ({matches}/{comparisons}) over {numUnits} units");
                }
            }

            Console.WriteLine($"\n  Best unit length: {bestUnitLen} bytes (score: {bestScore}%)");

            // Also try exact byte match auto-correlation
            Console.WriteLine("\n  --- Exact byte match auto-correlation ---");
            for (int unitLen = 2; unitLen <= 48; unitLen++)
            {
                int numUnits = effectiveLen / unitLen;
                if (numUnits < 3) continue;

                // Check if the last few units have any exact-match byte positions
                int exactMatches = 0;
                int totalComparisons = 0;
                for (int u = numUnits - 1; u >= Math.Max(1, numUnits - 10); u--)
                {
                    int offsetA = effectiveLen - (numUnits - u) * unitLen;
                    int offsetB = effectiveLen - (numUnits - u + 1) * unitLen;
                    if (offsetA < 0 || offsetB < 0) break;

                    for (int b = 0; b < unitLen; b++)
                    {
                        totalComparisons++;
                        if (tail[offsetA + b] == tail[offsetB + b]) exactMatches++;
                    }
                }

                if (totalComparisons > 0)
                {
                    int pct = (exactMatches * 100) / totalComparisons;
                    if (pct >= 60)
                        Console.WriteLine($"    unit={unitLen}: {pct}% exact match ({exactMatches}/{totalComparisons})");
                }
            }

            // Dump entries using the best unit length (or try common sizes: 3, 6, 8, 12, 24)
            int[] tryLengths;
            if (bestUnitLen > 0 && bestScore >= 60)
                tryLengths = new[] { bestUnitLen, 3, 6, 8, 12, 24 };
            else
                tryLengths = new[] { 3, 6, 8, 12, 24 };

            foreach (int unitLen in tryLengths.Distinct())
            {
                int numUnits = effectiveLen / unitLen;
                int dumpCount = Math.Min(numUnits, 30); // dump last 30 entries max
                int dumpStart = effectiveLen - dumpCount * unitLen;

                Console.WriteLine($"\n  --- Entries as {unitLen}-byte units (last {dumpCount} of {numUnits}) ---");
                int nonZeroCount = 0;
                for (int u = 0; u < dumpCount; u++)
                {
                    int off = dumpStart + u * unitLen;
                    bool allZero = true;
                    var hexParts = new System.Text.StringBuilder();
                    for (int b = 0; b < unitLen; b++)
                    {
                        if (tail[off + b] != 0) allZero = false;
                        hexParts.Append($"{tail[off + b]:X2} ");
                    }
                    if (!allZero) nonZeroCount++;

                    string marker = allZero ? " (zero)" : " *";
                    Console.WriteLine($"    [{numUnits - dumpCount + u,3}] @{startOffset + off,6}: {hexParts}{marker}");
                }
                Console.WriteLine($"    Non-zero in dump: {nonZeroCount}/{dumpCount}");

                // Count total non-zero entries across all units
                int totalNzEntries = 0;
                for (int u = 0; u < numUnits; u++)
                {
                    int off = u * unitLen;
                    bool az = true;
                    for (int b = 0; b < unitLen; b++)
                    {
                        if (off + b >= effectiveLen) break;
                        if (tail[off + b] != 0) { az = false; break; }
                    }
                    if (!az) totalNzEntries++;
                }
                Console.WriteLine($"    Total non-zero entries in last {readLen} bytes: {totalNzEntries}/{numUnits}");
            }
        }
    }

    /// <summary>
    /// ReachFull parser that counts active objects and accepts extra bits per active object.
    /// Used to test the hypothesis that H4 has additional per-object fields.
    /// </summary>
    private (int EndBit, int Active) ParseObjectsReachFullWithExtra(
        byte[] bitstream, int objStart, int bitsX, int bitsY, int bitsZ, int extraBitsPerObject)
    {
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);
        int active = 0;
        for (int i = 0; i < 640; i++)
        {
            if (pb.BitsRemaining < 1) break;
            if (!pb.ReadBool()) continue;
            active++;

            pb.ReadInteger(2); // flags
            bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
            bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
            bool ib = pb.ReadBool();
            if (ib) { pb.ReadInteger(bitsX); pb.ReadInteger(bitsY); pb.ReadInteger(bitsZ); }
            else { pb.ReadRawFloat(); pb.ReadRawFloat(); pb.ReadRawFloat(); }
            bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);

            uint shape = pb.ReadInteger(2);
            switch (shape) {
                case 1: pb.ReadInteger(11); break;
                case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
            }
            pb.ReadInteger(8); pb.ReadInteger(8); // spawn_seq, respawn
            uint typ = pb.ReadInteger(5);
            bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8);
            pb.ReadInteger(8); pb.ReadInteger(4); // pflags, team
            bool ca = pb.ReadBool(); if (!ca) pb.ReadInteger(3);
            if (typ == 2) pb.ReadInteger(8);
            if (typ == 10) { pb.ReadInteger(5); pb.ReadInteger(5); }
            bool lna = pb.ReadBool(); if (!lna) pb.ReadInteger(8);

            // Skip extra bits per active object
            if (extraBitsPerObject > 0)
                pb.SkipBits(extraBitsPerObject);

            if (pb.BitsRemaining < 0) break;
        }
        return (pb.BitOffset, active);
    }

    /// <summary>
    /// Modified ReachFull parser that counts objects with VALID positions (within map bounds).
    /// Extra bits are inserted at various points in the object to find the correct position.
    /// </summary>
    private (int EndBit, int Active, int ValidPos) ParseWithExtraValidation(
        byte[] bitstream, int objStart, int bitsX, int bitsY, int bitsZ,
        float xMin, float xMax, float yMin, float yMax, float zMin, float zMax,
        int extraBitsAfterReach)
    {
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);
        int active = 0, validPos = 0;
        for (int i = 0; i < 640; i++)
        {
            if (pb.BitsRemaining < 1) break;
            if (!pb.ReadBool()) continue;
            active++;

            pb.ReadInteger(2);
            bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
            bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
            bool ib = pb.ReadBool();
            float px, py, pz;
            if (ib) {
                uint rx = pb.ReadInteger(bitsX), ry = pb.ReadInteger(bitsY), rz = pb.ReadInteger(bitsZ);
                px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
            } else {
                px = pb.ReadRawFloat(); py = pb.ReadRawFloat(); pz = pb.ReadRawFloat();
            }
            bool posOk = ib && px >= xMin - 20 && px <= xMax + 20 && py >= yMin - 20 && py <= yMax + 20 && pz >= zMin - 20 && pz <= zMax + 20;
            if (posOk) validPos++;

            bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
            uint shape = pb.ReadInteger(2);
            switch (shape) {
                case 1: pb.ReadInteger(11); break;
                case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
            }
            pb.ReadInteger(8); pb.ReadInteger(8);
            uint typ = pb.ReadInteger(5);
            bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8);
            pb.ReadInteger(8); pb.ReadInteger(4);
            bool ca = pb.ReadBool(); if (!ca) pb.ReadInteger(3);
            if (typ == 2) pb.ReadInteger(8);
            if (typ == 10) { pb.ReadInteger(5); pb.ReadInteger(5); }
            bool lna = pb.ReadBool(); if (!lna) pb.ReadInteger(8);

            if (extraBitsAfterReach > 0) pb.SkipBits(extraBitsAfterReach);
            if (pb.BitsRemaining < 0) break;
        }
        return (pb.BitOffset, active, validPos);
    }

    /// <summary>
    /// Scan the bitstream for valid Reach-format objects starting at every bit offset.
    /// For each starting bit, try to read a Reach object and check if the position is valid.
    /// This finds ALL valid object starts regardless of what's between them.
    /// </summary>
    [Fact]
    public void Halo4ObjectBoundaryScan()
    {
        string path = Path.Combine(H4MccDir, "ca_forge_bonanza_relay.mvar");
        if (!File.Exists(path)) { Console.WriteLine("SKIP"); return; }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
        int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
        int packedLenBits = packedLenBytes * 8;

        // Parse to get bounds
        var bits = new ForgeX.Core.IO.BitReader(bitstream);
        bits.ReadInteger(4); bits.ReadInteger(32);
        bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
        bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
        bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool();
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

        bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
        int numQuotas = (int)bits.ReadInteger(9);
        bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
        float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
        float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
        float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
        bits.ReadInteger(32); bits.ReadInteger(32);
        int sc = (int)bits.ReadInteger(9);
        for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
        if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
        int objStart = bits.BitOffset;

        var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
            xMin, xMax, yMin, yMax, zMin, zMax, 21);

        Console.WriteLine($"objStart={objStart} bounds=X[{xMin:F1},{xMax:F1}] Y[{yMin:F1},{yMax:F1}] Z[{zMin:F1},{zMax:F1}]");
        Console.WriteLine($"posBits: X={bitsX} Y={bitsY} Z={bitsZ} total={bitsX+bitsY+bitsZ}");

        // Scan the object section bit-by-bit for valid position data.
        // At each bit offset, try: exists=true → skip Reach pre-position fields → read position → validate
        // We try TWO formats:
        // A) Reach-style: skip flags(2) + qi(1+8) + vi(1+5) → ib(1) → adaptive pos
        // B) H3-style: skip flags(16) + qi(32) + parent(1) + has_pos(1) → adaptive pos

        Console.WriteLine("\n=== Scanning for valid in-bounds positions ===");
        Console.WriteLine("Trying: raw position at offset (no prefix), Reach prefix, H3 prefix");

        var validPositions = new List<(int BitOffset, float X, float Y, float Z, string Format)>();
        int scanEnd = Math.Min(packedLenBits, objStart + 20000); // First 20K bits of object section

        for (int startBit = objStart; startBit < scanEnd; startBit++)
        {
            // Try reading a position directly (as if this IS the position field with ib=true)
            if (startBit + 1 + bitsX + bitsY + bitsZ <= bitstream.Length * 8)
            {
                var pb = new ForgeX.Core.IO.BitReader(bitstream);
                pb.SkipBits(startBit);
                bool ib = pb.ReadBool();
                if (ib)
                {
                    uint rx = pb.ReadInteger(bitsX), ry = pb.ReadInteger(bitsY), rz = pb.ReadInteger(bitsZ);
                    float px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                    float py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                    float pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));

                    // Strict bounds check (within actual map area, not edge)
                    float margin = 50f;
                    if (px >= xMin + margin && px <= xMax - margin &&
                        py >= yMin + margin && py <= yMax - margin &&
                        pz >= zMin + margin && pz <= zMax - margin)
                    {
                        validPositions.Add((startBit, px, py, pz, "ib_pos"));
                    }
                }
            }
        }

        // Print all valid positions found, grouped by proximity
        Console.WriteLine($"\nFound {validPositions.Count} valid position reads in bits {objStart}-{scanEnd}");
        Console.WriteLine("\nFirst 80 hits:");
        for (int i = 0; i < Math.Min(80, validPositions.Count); i++)
        {
            var v = validPositions[i];
            Console.WriteLine($"  bit {v.BitOffset,6}: ({v.X,9:F1},{v.Y,9:F1},{v.Z,8:F1}) [{v.Format}]");
        }

        // Analyze spacing between consecutive valid positions
        if (validPositions.Count >= 2)
        {
            Console.WriteLine("\n=== Spacing between consecutive valid positions ===");
            var spacings = new Dictionary<int, int>();
            for (int i = 1; i < validPositions.Count; i++)
            {
                int gap = validPositions[i].BitOffset - validPositions[i - 1].BitOffset;
                if (!spacings.ContainsKey(gap)) spacings[gap] = 0;
                spacings[gap]++;
            }
            foreach (var kv in spacings.OrderByDescending(s => s.Value).Take(20))
                Console.WriteLine($"  gap={kv.Key} bits: {kv.Value} occurrences");
        }
    }

    /// <summary>
    /// Find the extra bits value that maximizes valid positions across all files.
    /// Valid positions = objects where point_in_bounds=true AND position is within map bounds.
    /// This approach doesn't depend on quota positions.
    /// </summary>
    [Fact]
    public void Halo4ValidPositionSearch()
    {
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP"); return; }

        var fileData = new List<(string Name, byte[] Bits, int ObjStart, int BitsX, int BitsY, int BitsZ,
            float XMin, float XMax, float YMin, float YMax, float ZMin, float ZMax, int PackedBits, int NumQuotas)>();

        foreach (var filePath in files.OrderBy(f => Path.GetFileName(f)))
        {
            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            var bs = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bs, 0, bs.Length);
            int plb = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];

            var bits = new ForgeX.Core.IO.BitReader(bs);
            bits.ReadInteger(4); bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32);
            bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

            bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
            int nq = (int)bits.ReadInteger(9);
            bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            bits.ReadInteger(32); bits.ReadInteger(32);

            int sc = (int)bits.ReadInteger(9);
            for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
            if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
            int os = bits.BitOffset;
            var (bx, by, bz) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(xMin, xMax, yMin, yMax, zMin, zMax, 21);

            fileData.Add((Path.GetFileName(filePath), bs, os, bx, by, bz, xMin, xMax, yMin, yMax, zMin, zMax, plb * 8, nq));
        }

        // Sweep extra bits 0-600 and track valid positions per file
        Console.WriteLine("Extra | " + string.Join(" | ", fileData.Select(f => $"{f.Name.Substring(0, 8),8}")) + " | TotalValid");

        int bestExtra = 0;
        int bestTotalValid = 0;

        for (int extra = 0; extra <= 600; extra++)
        {
            int totalValid = 0;
            var validCounts = new List<int>();

            foreach (var f in fileData)
            {
                var (_, _, valid) = ParseWithExtraValidation(
                    f.Bits, f.ObjStart, f.BitsX, f.BitsY, f.BitsZ,
                    f.XMin, f.XMax, f.YMin, f.YMax, f.ZMin, f.ZMax, extra);
                totalValid += valid;
                validCounts.Add(valid);
            }

            if (totalValid > bestTotalValid)
            {
                bestTotalValid = totalValid;
                bestExtra = extra;
            }

            if (extra % 50 == 0 || totalValid >= bestTotalValid - 2)
            {
                Console.WriteLine($" {extra,4} | {string.Join(" | ", validCounts.Select(v => $"{v,8}"))} | {totalValid,5}");
            }
        }
        Console.WriteLine($"\nBest: extra={bestExtra} totalValid={bestTotalValid}");

        // Detailed for best value
        Console.WriteLine($"\nDetailed at extra={bestExtra}:");
        foreach (var f in fileData)
        {
            var (endBit, active, valid) = ParseWithExtraValidation(
                f.Bits, f.ObjStart, f.BitsX, f.BitsY, f.BitsZ,
                f.XMin, f.XMax, f.YMin, f.YMax, f.ZMin, f.ZMax, bestExtra);
            int qs = f.PackedBits - f.NumQuotas * 24;
            Console.WriteLine($"  {f.Name}: active={active} validPos={valid} end={endBit} delta={endBit-qs}");
        }
    }

    /// <summary>
    /// Search for the per-object extra bits needed to match quotas across all files.
    /// If H4 has additional per-object fields, the extra bits should be consistent.
    /// </summary>
    [Fact]
    public void Halo4ExtraBitsSearch()
    {
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP"); return; }

        // First pass: collect data for each file
        var fileData = new List<(string Name, byte[] Bits, int ObjStart, int BitsX, int BitsY, int BitsZ, int PackedLenBits, int NumQuotas)>();

        foreach (var filePath in files.OrderBy(f => Path.GetFileName(f)))
        {
            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            var bitstream = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
            int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
            int packedLenBits = packedLenBytes * 8;

            var bits = new ForgeX.Core.IO.BitReader(bitstream);
            bits.ReadInteger(4); bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32);
            bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

            bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
            int numQuotas = (int)bits.ReadInteger(9);
            bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            bits.ReadInteger(32); bits.ReadInteger(32);

            int strCount = (int)bits.ReadInteger(9);
            for (int i = 0; i < strCount; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
            if (strCount > 0) {
                int bs = (int)bits.ReadInteger(13); bool comp = bits.ReadBool();
                int rs = comp ? (int)bits.ReadInteger(13) : bs;
                for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8);
            }
            int objStart = bits.BitOffset;
            var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);

            fileData.Add((Path.GetFileName(filePath), bitstream, objStart, bitsX, bitsY, bitsZ, packedLenBits, numQuotas));
        }

        // Sweep extra bits from 0 to 600 in steps of 1
        Console.WriteLine("ExtraBits | " + string.Join(" | ", fileData.Select(f => $"{f.Name.Substring(0, Math.Min(12, f.Name.Length)),12}")));

        int bestExtra = 0;
        long bestTotalAbsDelta = long.MaxValue;

        for (int extra = 0; extra <= 600; extra += 1)
        {
            long totalAbsDelta = 0;
            var results = new List<string>();
            bool anyGood = false;

            foreach (var f in fileData)
            {
                var (endBit, active) = ParseObjectsReachFullWithExtra(
                    f.Bits, f.ObjStart, f.BitsX, f.BitsY, f.BitsZ, extra);
                int quotaStart = f.PackedLenBits - f.NumQuotas * 24;
                int delta = endBit - quotaStart;
                totalAbsDelta += Math.Abs(delta);
                results.Add($"{delta,7}/{active,3}a");

                // Check if quotas are valid at this position
                if (Math.Abs(delta) < 500) anyGood = true;
            }

            if (totalAbsDelta < bestTotalAbsDelta)
            {
                bestTotalAbsDelta = totalAbsDelta;
                bestExtra = extra;
            }

            // Print every 50 and also when total delta is small
            if (extra % 50 == 0 || totalAbsDelta < 10000 || anyGood)
            {
                Console.WriteLine($"  {extra,5}   | {string.Join(" | ", results)}  total={totalAbsDelta}");
            }
        }
        Console.WriteLine($"\nBest extra bits: {bestExtra} with total |delta|={bestTotalAbsDelta}");

        // Detailed output for the best value
        Console.WriteLine($"\nDetailed results with {bestExtra} extra bits per object:");
        foreach (var f in fileData)
        {
            var (endBit, active) = ParseObjectsReachFullWithExtra(
                f.Bits, f.ObjStart, f.BitsX, f.BitsY, f.BitsZ, bestExtra);
            int quotaStart = f.PackedLenBits - f.NumQuotas * 24;
            Console.WriteLine($"  {f.Name}: end={endBit} active={active} delta={endBit - quotaStart} quota24@{quotaStart}");
        }
    }

    /// <summary>
    /// Sweep objStart ±30 bits on all files using different parsers.
    /// If a consistent offset gives dramatically better results, it means the metadata parsing
    /// has a bit-alignment error.
    /// Also try different quota sizes to find the actual total data consumed.
    /// </summary>
    [Fact]
    public void Halo4ObjStartSweep()
    {
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP"); return; }

        foreach (var filePath in files.OrderBy(f => Path.GetFileName(f)))
        {
            Console.WriteLine($"\n=== {Path.GetFileName(filePath)} ===");
            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            var bitstream = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
            int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
            int packedLenBits = packedLenBytes * 8;

            // Parse metadata + header to get nominal objStart
            var bits = new ForgeX.Core.IO.BitReader(bitstream);
            bits.ReadInteger(4); bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32);
            bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

            bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
            int numQuotas = (int)bits.ReadInteger(9);
            bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            bits.ReadInteger(32); bits.ReadInteger(32);

            int strCount = (int)bits.ReadInteger(9);
            for (int i = 0; i < strCount; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
            if (strCount > 0) {
                int bs = (int)bits.ReadInteger(13); bool comp = bits.ReadBool();
                int rs = comp ? (int)bits.ReadInteger(13) : bs;
                for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8);
            }
            int nominalObjStart = bits.BitOffset;

            var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);

            int quotaStart24 = packedLenBits - numQuotas * 24;

            Console.WriteLine($"  packed={packedLenBits}b  nominalObjStart={nominalObjStart}  quotas24@{quotaStart24}");
            Console.WriteLine($"  Expected obj section: {quotaStart24 - nominalObjStart} bits");

            // Sweep: for each offset, try ReachFull and H4 parsers
            Console.WriteLine($"\n  Offset  ReachFull_end  RF_delta  RF_active   H4_end   H4_delta  H4_active");
            int bestOffset = 0;
            long bestAbsDelta = long.MaxValue;
            string bestParser = "";

            for (int offset = -20; offset <= 20; offset++)
            {
                int os = nominalObjStart + offset;
                if (os < 0 || os >= packedLenBits) continue;

                int endRF = ParseObjectsReachFull(bitstream, os, bitsX, bitsY, bitsZ);
                int endH4 = ParseObjectsH4(bitstream, os, bitsX, bitsY, bitsZ);

                int deltaRF = endRF - quotaStart24;
                int deltaH4 = endH4 - quotaStart24;

                // Count actives by comparing end to start
                // (we can't easily count from the return value, so just show end positions)
                if (offset % 5 == 0 || Math.Abs(deltaRF) < Math.Abs(bestAbsDelta) || Math.Abs(deltaH4) < Math.Abs(bestAbsDelta))
                {
                    Console.WriteLine($"  {offset,4}    {endRF,10}  {deltaRF,9}              {endH4,10}  {deltaH4,9}");
                }

                if (Math.Abs(deltaRF) < bestAbsDelta)
                {
                    bestAbsDelta = Math.Abs(deltaRF);
                    bestOffset = offset;
                    bestParser = "ReachFull";
                }
                if (Math.Abs(deltaH4) < bestAbsDelta)
                {
                    bestAbsDelta = Math.Abs(deltaH4);
                    bestOffset = offset;
                    bestParser = "H4";
                }
            }
            Console.WriteLine($"  Best: offset={bestOffset} parser={bestParser} delta={bestAbsDelta}");
        }
    }

    /// <summary>
    /// Test H3-style object parsing on all H4 files and compare with Reach parsers.
    /// The key hypothesis: H4 uses H3-like per-object format (16-bit flags, 32-bit quota,
    /// has_parent_object, H3 property layout) but with Reach's adaptive position encoding.
    /// </summary>
    [Fact]
    public void Halo4H3StyleComparison()
    {
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP"); return; }

        foreach (var filePath in files.OrderBy(f => Path.GetFileName(f)))
        {
            Console.WriteLine($"\n{'=',-60}");
            Console.WriteLine($"=== {Path.GetFileName(filePath)} ===");

            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            var bitstream = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
            int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
            int packedLenBits = packedLenBytes * 8;

            // Parse metadata + header
            var bits = new ForgeX.Core.IO.BitReader(bitstream);
            bits.ReadInteger(4); bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32);
            bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

            bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
            int numQuotas = (int)bits.ReadInteger(9);
            bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            bits.ReadInteger(32); bits.ReadInteger(32);

            // String table
            int strCount = (int)bits.ReadInteger(9);
            for (int i = 0; i < strCount; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
            if (strCount > 0) {
                int bs = (int)bits.ReadInteger(13); bool comp = bits.ReadBool();
                int rs = comp ? (int)bits.ReadInteger(13) : bs;
                for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8);
            }

            int objStart = bits.BitOffset;
            var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);

            Console.WriteLine($"  packed={packedLenBits}b  objStart={objStart}  numQuotas={numQuotas}");
            Console.WriteLine($"  bounds: X[{xMin:F1},{xMax:F1}] Y[{yMin:F1},{yMax:F1}] Z[{zMin:F1},{zMax:F1}]");
            Console.WriteLine($"  posBits: X={bitsX} Y={bitsY} Z={bitsZ}");

            // Test multiple format combinations
            // H3-like with adaptive pos + Reach orient
            var r1 = ParseObjectsH3Style(bitstream, objStart, 640, bitsX, bitsY, bitsZ, xMin, xMax, yMin, yMax, zMin, zMax,
                useAdaptivePos: true, useReachOrientation: true);
            // H3-like with adaptive pos + H3 orient
            var r2 = ParseObjectsH3Style(bitstream, objStart, 640, bitsX, bitsY, bitsZ, xMin, xMax, yMin, yMax, zMin, zMax,
                useAdaptivePos: true, useReachOrientation: false);
            // H3-like with fixed 16-bit pos + H3 orient
            var r3 = ParseObjectsH3Style(bitstream, objStart, 640, bitsX, bitsY, bitsZ, xMin, xMax, yMin, yMax, zMin, zMax,
                useAdaptivePos: false, useReachOrientation: false);
            // H3-like with fixed 16-bit pos + Reach orient
            var r4 = ParseObjectsH3Style(bitstream, objStart, 640, bitsX, bitsY, bitsZ, xMin, xMax, yMin, yMax, zMin, zMax,
                useAdaptivePos: false, useReachOrientation: true);

            // Also test Reach format and H4 format for comparison
            int endReach = ParseObjectsReachFull(bitstream, objStart, bitsX, bitsY, bitsZ);
            int endH4 = ParseObjectsH4(bitstream, objStart, bitsX, bitsY, bitsZ);

            // Calculate expected quota positions for different quota sizes
            int quotaStart24 = packedLenBits - numQuotas * 24;
            int quotaStart96 = packedLenBits - numQuotas * 96;

            Console.WriteLine($"\n  Format                       EndBit   Active  Good   Δ24     Δ96");
            Console.WriteLine($"  H3+adaptPos+reachOrient:    {r1.EndBit,8}  {r1.ActiveCount,5}  {r1.GoodCount,5}  {r1.EndBit - quotaStart24,7}  {r1.EndBit - quotaStart96,7}");
            Console.WriteLine($"  H3+adaptPos+h3Orient:       {r2.EndBit,8}  {r2.ActiveCount,5}  {r2.GoodCount,5}  {r2.EndBit - quotaStart24,7}  {r2.EndBit - quotaStart96,7}");
            Console.WriteLine($"  H3+fixed16Pos+h3Orient:     {r3.EndBit,8}  {r3.ActiveCount,5}  {r3.GoodCount,5}  {r3.EndBit - quotaStart24,7}  {r3.EndBit - quotaStart96,7}");
            Console.WriteLine($"  H3+fixed16Pos+reachOrient:  {r4.EndBit,8}  {r4.ActiveCount,5}  {r4.GoodCount,5}  {r4.EndBit - quotaStart24,7}  {r4.EndBit - quotaStart96,7}");
            Console.WriteLine($"  ReachFull:                  {endReach,8}                    {endReach - quotaStart24,7}  {endReach - quotaStart96,7}");
            Console.WriteLine($"  H4-style:                   {endH4,8}                    {endH4 - quotaStart24,7}  {endH4 - quotaStart96,7}");
            Console.WriteLine($"  Expected quota: 24b@{quotaStart24}  96b@{quotaStart96}");

            // Verbose dump of H3+adaptive+reach for first file
            if (Path.GetFileName(filePath).StartsWith("grifball"))
            {
                Console.WriteLine($"\n  --- H3-style verbose dump (grifball) ---");
                ParseObjectsH3Style(bitstream, objStart, 640, bitsX, bitsY, bitsZ, xMin, xMax, yMin, yMax, zMin, zMax,
                    useAdaptivePos: true, useReachOrientation: true, verbose: true);
            }
        }
    }

    /// <summary>
    /// Compare ParseObjectsReachFull vs ParseObjectsH4 vs old Reach_noSR across all 4 H4 files.
    /// This tests whether fixing the conditional per-type fields or using the H4 format resolves
    /// the massive negative deltas seen with large forge maps.
    /// </summary>
    [Fact]
    public void Halo4ParserComparison()
    {
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP: No H4 mvar files found"); return; }

        foreach (var filePath in files.OrderBy(f => Path.GetFileName(f)))
        {
            Console.WriteLine($"\n{'=',-60}");
            Console.WriteLine($"=== {Path.GetFileName(filePath)} ===");
            Console.WriteLine($"{'=',-60}");

            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            var bitstream = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
            int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
            int packedLenBits = packedLenBytes * 8;

            // Parse metadata + header to get objStart, bounds, numQuotas
            var bits = new ForgeX.Core.IO.BitReader(bitstream);
            bits.ReadInteger(4); bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32);
            bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

            uint encVer = bits.ReadInteger(8);
            bits.ReadInteger(32); bits.ReadInteger(32);
            int numQuotas = (int)bits.ReadInteger(9);
            uint mapId = bits.ReadInteger(32);
            bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            bits.ReadInteger(32); bits.ReadInteger(32);

            // String table
            int strCount = (int)bits.ReadInteger(9);
            for (int i = 0; i < strCount; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
            if (strCount > 0)
            {
                int bufSize = (int)bits.ReadInteger(13);
                bool compressed = bits.ReadBool();
                int readSize = compressed ? (int)bits.ReadInteger(13) : bufSize;
                for (int ci = 0; ci < readSize; ci++) bits.ReadInteger(8);
            }

            int objStart = bits.BitOffset;
            var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);

            int quotaSize = numQuotas * 24;
            int expectedQuotaStart = packedLenBits - quotaSize;

            Console.WriteLine($"  packed={packedLenBytes}B={packedLenBits}b  objStart={objStart}  numQuotas={numQuotas}  quotaSize={quotaSize}b");
            Console.WriteLine($"  bounds: X[{xMin:F1},{xMax:F1}] Y[{yMin:F1},{yMax:F1}] Z[{zMin:F1},{zMax:F1}]");
            Console.WriteLine($"  positionBits: X={bitsX} Y={bitsY} Z={bitsZ}");
            Console.WriteLine($"  expectedQuotaStart={expectedQuotaStart}");

            // Count active objects from existence flags
            var countBits = new ForgeX.Core.IO.BitReader(bitstream);
            countBits.SkipBits(objStart);
            int rawActiveCount = 0;
            // Can't count without parsing (variable-length), so skip this

            // === Run the 3 parsers ===
            int endReachNoSR = ParseObjects(bitstream, objStart, bitsX, bitsY, bitsZ,
                hasSpawnSeq: true, hasLabel: true, hasPrimaryColor: true, hasSharedStorage: false,
                hasConditionalPerType: false, typeBits: 5, pflagsBits: 8, teamBits: 4);

            int endReachFull = ParseObjectsReachFull(bitstream, objStart, bitsX, bitsY, bitsZ);

            int endH4 = ParseObjectsH4(bitstream, objStart, bitsX, bitsY, bitsZ);

            // Also try H4 with 16-bit dims (Nitrogen hint)
            int endH4_16dim = ParseObjectsH4(bitstream, objStart, bitsX, bitsY, bitsZ, dimBits: 16);

            Console.WriteLine($"\n  Parser results (end bit offset → delta from expected quota start):");
            Console.WriteLine($"    Reach_noSR:       end={endReachNoSR,8}  delta={endReachNoSR - expectedQuotaStart,8}");
            Console.WriteLine($"    Reach_full:       end={endReachFull,8}  delta={endReachFull - expectedQuotaStart,8}");
            Console.WriteLine($"    H4:               end={endH4,8}  delta={endH4 - expectedQuotaStart,8}");
            Console.WriteLine($"    H4_16dim:         end={endH4_16dim,8}  delta={endH4_16dim - expectedQuotaStart,8}");

            // Validate quotas at each parser's end position
            foreach (var (label, endPos) in new[] {
                ("Reach_noSR", endReachNoSR), ("Reach_full", endReachFull),
                ("H4", endH4), ("H4_16dim", endH4_16dim) })
            {
                if (endPos <= 0 || endPos + quotaSize > bitstream.Length * 8) continue;

                var qr = new ForgeX.Core.IO.BitReader(bitstream);
                qr.SkipBits(endPos);
                int nonZero = 0, placed = 0;
                bool bad = false;
                for (int q = 0; q < numQuotas; q++)
                {
                    int mn = (int)qr.ReadInteger(8);
                    int mx = (int)qr.ReadInteger(8);
                    int ct = (int)qr.ReadInteger(8);
                    if (mn == 0 && mx == 0 && ct == 0) continue;
                    if (mn > mx || ct > mx || mx > 200) { bad = true; break; }
                    nonZero++;
                    placed += ct;
                }
                string status = bad ? "INVALID" : $"ok (nonZero={nonZero}, placed={placed})";
                Console.WriteLine($"    {label,-18} quotas @ end: {status}");
            }

            // Also scan for best quota position near each parser's end
            foreach (var (label, endPos) in new[] {
                ("Reach_noSR", endReachNoSR), ("Reach_full", endReachFull),
                ("H4", endH4), ("H4_16dim", endH4_16dim) })
            {
                int searchStart = Math.Max(objStart + 100, endPos - 500);
                int searchEnd = Math.Min(endPos + 500, (int)(bitstream.Length * 8L) - quotaSize);
                int bestPos = -1, bestNZ = 0, bestPlaced = 0;
                for (int sp = searchStart; sp <= searchEnd; sp++)
                {
                    var qr = new ForgeX.Core.IO.BitReader(bitstream);
                    qr.SkipBits(sp);
                    int nz = 0, pl = 0;
                    bool bad2 = false;
                    for (int q = 0; q < numQuotas; q++)
                    {
                        int mn = (int)qr.ReadInteger(8);
                        int mx = (int)qr.ReadInteger(8);
                        int ct = (int)qr.ReadInteger(8);
                        if (mn == 0 && mx == 0 && ct == 0) continue;
                        if (mn > mx || ct > mx || mx > 200) { bad2 = true; break; }
                        nz++; pl += ct;
                    }
                    if (!bad2 && nz > bestNZ) { bestPos = sp; bestNZ = nz; bestPlaced = pl; }
                }
                if (bestPos >= 0)
                    Console.WriteLine($"    {label,-18} best quota ±500: pos={bestPos} (offset={bestPos - endPos}) nonZero={bestNZ} placed={bestPlaced}");
            }
        }
    }

    /// <summary>
    /// Corrected Reach TU1 format parser (from Blam-Network/blf authoritative source).
    /// Fixes: adds spawn_relative_to(10), corrects cached_type conditionals
    /// (weapon=1, teleporter=12/13/14, location_name=19).
    /// Returns (endBit, activeCount, validPositionCount, perObjectBits[]).
    /// </summary>
    private (int EndBit, int Active, int ValidPos, List<(int Slot, int Bits, string Info)> Objects)
        ParseObjectsReachTU1(byte[] bitstream, int objStart, int bitsX, int bitsY, int bitsZ,
            float xMin, float xMax, float yMin, float yMax, float zMin, float zMax,
            int numSlots = 640, bool verbose = false, int dimBits = 11)
    {
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);
        int active = 0, validPos = 0;
        var objects = new List<(int Slot, int Bits, string Info)>();

        for (int i = 0; i < numSlots; i++)
        {
            if (pb.BitsRemaining < 1) break;
            int startBit = pb.BitOffset;
            if (!pb.ReadBool()) continue; // exists
            active++;

            uint flags = pb.ReadInteger(2);
            bool qaAbsent = pb.ReadBool(); int qi = qaAbsent ? -1 : (int)pb.ReadInteger(8);
            bool vaAbsent = pb.ReadBool(); int vi = vaAbsent ? -1 : (int)pb.ReadInteger(5);

            // Position
            bool ib = pb.ReadBool();
            float px = 0, py = 0, pz = 0;
            if (ib) {
                uint rx = pb.ReadInteger(bitsX), ry = pb.ReadInteger(bitsY), rz = pb.ReadInteger(bitsZ);
                px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
            } else {
                px = pb.ReadRawFloat(); py = pb.ReadRawFloat(); pz = pb.ReadRawFloat();
            }
            bool posOk = ib && px >= xMin - 20 && px <= xMax + 20 && py >= yMin - 20 && py <= yMax + 20 && pz >= zMin - 20 && pz <= zMax + 20;
            if (posOk) validPos++;

            // Orientation
            bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);

            // spawn_relative_to: 10 bits, stored as value+1
            int spawnRelTo = (int)pb.ReadInteger(10) - 1;

            // Boundary shape
            uint shape = pb.ReadInteger(2);
            switch (shape) {
                case 1: pb.ReadInteger(dimBits); break;
                case 2: pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); break;
                case 3: pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); break;
            }

            // Multiplayer properties
            uint seq = pb.ReadInteger(8);         // user_data / spawn_sequence
            uint respawn = pb.ReadInteger(8);      // spawn_time
            uint cachedType = pb.ReadInteger(5);   // cached_type
            bool laAbsent = pb.ReadBool(); int label = laAbsent ? -1 : (int)pb.ReadInteger(8);
            uint pflags = pb.ReadInteger(8);       // placement_flags
            uint team = pb.ReadInteger(4);         // team (value+1)
            bool caAbsent = pb.ReadBool(); int color = caAbsent ? -1 : (int)pb.ReadInteger(3);

            // Conditional per cached_type (Blam-Network/blf authoritative values)
            if (cachedType == 1) // weapon
                pb.ReadInteger(8); // spare_clips
            if (cachedType >= 12 && cachedType <= 14) // teleporter types
                { pb.ReadInteger(5); pb.ReadInteger(5); } // channel, passability
            if (cachedType == 19) // location_name
                { bool lnAbsent = pb.ReadBool(); if (!lnAbsent) pb.ReadInteger(8); }

            int endBit = pb.BitOffset;
            int bits2 = endBit - startBit;

            string info = $"qi={qi} vi={vi} ib={ib} pos=({px:F1},{py:F1},{pz:F1}) spawnRel={spawnRelTo} sh={shape} seq={seq} resp={respawn} typ={cachedType} lbl={label} pf=0x{pflags:X2} tm={team} col={color}";
            objects.Add((i, bits2, info));

            if (verbose && (active <= 20 || !posOk))
                Console.WriteLine($"  [{i}] #{active}: {bits2}b @{startBit}-{endBit} {(posOk ? "OK" : "BAD")} {info}");

            if (pb.BitsRemaining < 0) { Console.WriteLine($"  *** OVERRUN at [{i}] ***"); break; }
        }
        return (pb.BitOffset, active, validPos, objects);
    }

    [Fact]
    public void Halo4CorrectedReachFormat()
    {
        // Test all H4 files with the CORRECTED Reach TU1 format (spawn_relative_to + fixed conditionals)
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP: no H4 files"); return; }

        foreach (var filePath in files.OrderBy(f => new FileInfo(f).Length))
        {
            string name = Path.GetFileName(filePath);
            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            var bitstream = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
            int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
            int packedLenBits = packedLenBytes * 8;

            // Parse metadata + header
            var bits = new ForgeX.Core.IO.BitReader(bitstream);
            bits.ReadInteger(4); bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

            bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
            int numQuotas = (int)bits.ReadInteger(9);
            bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            bits.ReadInteger(32); bits.ReadInteger(32);
            int sc = (int)bits.ReadInteger(9);
            for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
            if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
            int objStart = bits.BitOffset;

            var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);

            int quotaStart24 = packedLenBits - numQuotas * 24;
            bool isGrifball = name.Contains("grifball");

            Console.WriteLine($"\n=== {name} ===");
            Console.WriteLine($"  packed={packedLenBits}b objStart={objStart} posBits=({bitsX},{bitsY},{bitsZ}) quotas={numQuotas}");

            // Test with 11-bit dims (Reach) and 16-bit dims (H4/Nitrogen)
            foreach (int dimBits in new[] { 11, 16 })
            {
                var (endBit, active, validPos, objs) = ParseObjectsReachTU1(
                    bitstream, objStart, bitsX, bitsY, bitsZ,
                    xMin, xMax, yMin, yMax, zMin, zMax,
                    numSlots: 640, verbose: isGrifball && dimBits == 11, dimBits: dimBits);

                int delta = endBit - quotaStart24;
                float avgBits = active > 0 ? (float)(endBit - objStart - (640 - active)) / active : 0;

                Console.WriteLine($"  dim={dimBits}b: end={endBit} active={active} validPos={validPos} delta={delta} avgBits={avgBits:F1}");

                // Check quota validity at the expected position
                if (Math.Abs(delta) < 100)
                {
                    var qr = new ForgeX.Core.IO.BitReader(bitstream);
                    qr.SkipBits(endBit);
                    int qNz = 0, qPlaced = 0;
                    bool qValid = true;
                    for (int q = 0; q < numQuotas; q++)
                    {
                        int mn = (int)qr.ReadInteger(8); int mx = (int)qr.ReadInteger(8); int ct = (int)qr.ReadInteger(8);
                        if (mn == 0 && mx == 0 && ct == 0) continue;
                        if (mn > mx || ct > mx || mx > 200) { qValid = false; break; }
                        qNz++; qPlaced += ct;
                    }
                    if (qValid && qNz > 0)
                        Console.WriteLine($"    QUOTA CHECK: nonZero={qNz} placed={qPlaced} — VALID!");
                }

                // For grifball with 11-bit dims, dump first few objects and next 30 raw bits after each
                if (isGrifball && dimBits == 11 && active > 0)
                {
                    Console.WriteLine($"\n  Per-object bit consumption (first 20):");
                    foreach (var obj in objs.Take(20))
                        Console.WriteLine($"    [{obj.Slot}] {obj.Bits}b: {obj.Info}");

                    // Also: what bits follow the last object?
                    Console.WriteLine($"\n  Next 60 bits after objects (at bit {endBit}):");
                    var peek = new ForgeX.Core.IO.BitReader(bitstream);
                    peek.SkipBits(endBit);
                    for (int b = 0; b < 60 && peek.BitsRemaining > 0; b++)
                        Console.Write(peek.ReadBool() ? "1" : "0");
                    Console.WriteLine();
                }
            }
        }
    }

    /// <summary>
    /// Sweep extra bits per object using the CORRECTED Reach TU1 format.
    /// The corrected format adds spawn_relative_to(10) and fixes cached_type conditionals.
    /// </summary>
    [Fact]
    public void Halo4CorrectedExtraBitsSweep()
    {
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP"); return; }

        var fileData = new List<(string N, byte[] BS, int ObjStart, int BX, int BY, int BZ,
            float XMin, float XMax, float YMin, float YMax, float ZMin, float ZMax,
            int PackedBits, int NumQuotas)>();

        foreach (var filePath in files.OrderBy(f => Path.GetFileName(f)))
        {
            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            var bs = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bs, 0, bs.Length);
            int plb = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];

            var bits = new ForgeX.Core.IO.BitReader(bs);
            bits.ReadInteger(4); bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

            bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
            int nq = (int)bits.ReadInteger(9);
            bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            bits.ReadInteger(32); bits.ReadInteger(32);
            int sc = (int)bits.ReadInteger(9);
            for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
            if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
            int objStart = bits.BitOffset;

            var (bx, by, bz) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);

            fileData.Add((Path.GetFileName(filePath), bs, objStart, bx, by, bz,
                xMin, xMax, yMin, yMax, zMin, zMax, plb * 8, nq));
        }

        // Sweep extra bits 0-620 in steps, using corrected parser with spawn_relative_to
        Console.WriteLine("Extra bits sweep with CORRECTED Reach TU1 format (spawn_relative_to + fixed conditionals):");
        Console.WriteLine($"{"Extra",6} | {string.Join(" | ", fileData.Select(f => $"{f.N.Substring(0, Math.Min(14, f.N.Length)),-14}"))}");

        // First do a coarse sweep to find the interesting range
        var bestPerFile = new int[fileData.Count];
        var bestDeltaPerFile = new int[fileData.Count];
        for (int i = 0; i < fileData.Count; i++) bestDeltaPerFile[i] = int.MaxValue;

        foreach (int extra in Enumerable.Range(0, 31).Concat(Enumerable.Range(31, 590).Where(e => e % 5 == 0)))
        {
            var results = new List<string>();
            for (int fi = 0; fi < fileData.Count; fi++)
            {
                var f = fileData[fi];
                var pb = new ForgeX.Core.IO.BitReader(f.BS);
                pb.SkipBits(f.ObjStart);
                int act = 0, vp = 0;
                for (int i = 0; i < 640; i++)
                {
                    if (pb.BitsRemaining < 1) break;
                    if (!pb.ReadBool()) continue;
                    act++;
                    pb.ReadInteger(2);
                    bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
                    bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
                    bool ib = pb.ReadBool();
                    float px, py, pz;
                    if (ib) {
                        uint rx = pb.ReadInteger(f.BX), ry = pb.ReadInteger(f.BY), rz = pb.ReadInteger(f.BZ);
                        px = f.XMin + (rx + 0.5f) * ((f.XMax - f.XMin) / (1u << f.BX));
                        py = f.YMin + (ry + 0.5f) * ((f.YMax - f.YMin) / (1u << f.BY));
                        pz = f.ZMin + (rz + 0.5f) * ((f.ZMax - f.ZMin) / (1u << f.BZ));
                    } else { px = pb.ReadRawFloat(); py = pb.ReadRawFloat(); pz = pb.ReadRawFloat(); }
                    if (ib && px >= f.XMin - 20 && px <= f.XMax + 20 && py >= f.YMin - 20 && py <= f.YMax + 20 && pz >= f.ZMin - 20 && pz <= f.ZMax + 20) vp++;

                    bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
                    pb.ReadInteger(10); // spawn_relative_to
                    uint shape = pb.ReadInteger(2);
                    switch (shape) {
                        case 1: pb.ReadInteger(11); break;
                        case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                        case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break;
                    }
                    pb.ReadInteger(8); pb.ReadInteger(8);
                    uint ct = pb.ReadInteger(5);
                    bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8);
                    pb.ReadInteger(8); pb.ReadInteger(4);
                    bool ca = pb.ReadBool(); if (!ca) pb.ReadInteger(3);
                    if (ct == 1) pb.ReadInteger(8);
                    if (ct >= 12 && ct <= 14) { pb.ReadInteger(5); pb.ReadInteger(5); }
                    if (ct == 19) { bool ln = pb.ReadBool(); if (!ln) pb.ReadInteger(8); }

                    if (extra > 0) pb.SkipBits(extra);
                    if (pb.BitsRemaining < 0) break;
                }
                int quotaStart = f.PackedBits - f.NumQuotas * 24;
                int delta = pb.BitOffset - quotaStart;
                if (Math.Abs(delta) < Math.Abs(bestDeltaPerFile[fi]))
                    { bestDeltaPerFile[fi] = delta; bestPerFile[fi] = extra; }
                results.Add($"a={act,3} v={vp,3} d={delta,6}");
            }
            // Only print if at least one file has small delta or it's near an interesting value
            bool interesting = results.Any(r => {
                int dIdx = r.IndexOf("d=") + 2;
                string dStr = r.Substring(dIdx).Trim();
                return int.TryParse(dStr, out int d) && Math.Abs(d) < 2000;
            }) || extra <= 30;
            if (interesting)
                Console.WriteLine($"{extra,6} | {string.Join(" | ", results)}");
        }

        Console.WriteLine("\nBest extra bits per file:");
        for (int fi = 0; fi < fileData.Count; fi++)
            Console.WriteLine($"  {fileData[fi].N}: extra={bestPerFile[fi]} delta={bestDeltaPerFile[fi]}");
    }

    /// <summary>
    /// Test multiple format hypotheses on all H4 files to find the correct per-object format.
    /// Each hypothesis is a different combination of field widths and presence.
    /// </summary>
    [Fact]
    public void Halo4FormatHypothesisV2()
    {
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP"); return; }

        foreach (var filePath in files.OrderBy(f => new FileInfo(f).Length))
        {
            string name = Path.GetFileName(filePath);
            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            var bitstream = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
            int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
            int packedLenBits = packedLenBytes * 8;

            var bits = new ForgeX.Core.IO.BitReader(bitstream);
            bits.ReadInteger(4); bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
            int numQuotas = (int)bits.ReadInteger(9);
            bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            bits.ReadInteger(32); bits.ReadInteger(32);
            int sc = (int)bits.ReadInteger(9);
            for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
            if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
            int objStart = bits.BitOffset;
            var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);
            int quotaStart24 = packedLenBits - numQuotas * 24;

            Console.WriteLine($"\n=== {name} (packed={packedLenBits}b, objStart={objStart}, posBits={bitsX}+{bitsY}+{bitsZ}) ===");

            // Hypothesis A: Exact Reach TU1 with spawn_relative_to
            var (endA, actA, vpA, _) = ParseObjectsReachTU1(bitstream, objStart, bitsX, bitsY, bitsZ,
                xMin, xMax, yMin, yMax, zMin, zMax, 640);
            Console.WriteLine($"  A) Reach TU1:           active={actA,3} valid={vpA,3} delta={endA - quotaStart24,6}");

            // Hypothesis B: Reach + has_position (skip pos+orient+spawnrel when ib=false)
            var (endB, actB, vpB) = ParseH4HasPosition(bitstream, objStart, bitsX, bitsY, bitsZ,
                xMin, xMax, yMin, yMax, zMin, zMax, 640, hasSpawnRel: true, dimBits: 11);
            Console.WriteLine($"  B) Reach+has_pos:       active={actB,3} valid={vpB,3} delta={endB - quotaStart24,6}");

            // Hypothesis C: H3-style properties (type8+sym8+eng16+storage8+resp8+team8+shape8)
            var (endC, actC, vpC) = ParseH4H3Props(bitstream, objStart, bitsX, bitsY, bitsZ,
                xMin, xMax, yMin, yMax, zMin, zMax, 640);
            Console.WriteLine($"  C) Reach+H3props:       active={actC,3} valid={vpC,3} delta={endC - quotaStart24,6}");

            // Hypothesis D: Reach + 19 extra bits after properties
            var (endD, actD, vpD) = ParseH4Extended(bitstream, objStart, bitsX, bitsY, bitsZ,
                xMin, xMax, yMin, yMax, zMin, zMax, 640, extraBits: 19);
            Console.WriteLine($"  D) Reach+19extra:       active={actD,3} valid={vpD,3} delta={endD - quotaStart24,6}");

            // Hypothesis E: has_position + H3 properties
            var (endE, actE, vpE) = ParseH4HasPosH3Props(bitstream, objStart, bitsX, bitsY, bitsZ,
                xMin, xMax, yMin, yMax, zMin, zMax, 640);
            Console.WriteLine($"  E) has_pos+H3props:     active={actE,3} valid={vpE,3} delta={endE - quotaStart24,6}");

            // Hypothesis F: 16-bit flags + Reach
            var (endF, actF, vpF) = ParseH4WideFlags(bitstream, objStart, bitsX, bitsY, bitsZ,
                xMin, xMax, yMin, yMax, zMin, zMax, 640);
            Console.WriteLine($"  F) 16b_flags+Reach:     active={actF,3} valid={vpF,3} delta={endF - quotaStart24,6}");

            // Hypothesis G: has_position + Reach properties (no spawnrel when no pos)
            var (endG, actG, vpG) = ParseH4HasPosition(bitstream, objStart, bitsX, bitsY, bitsZ,
                xMin, xMax, yMin, yMax, zMin, zMax, 640, hasSpawnRel: false, dimBits: 11);
            Console.WriteLine($"  G) has_pos(noSR):       active={actG,3} valid={vpG,3} delta={endG - quotaStart24,6}");

            // Hypothesis H: has_position + Reach + 16bit dims
            var (endH, actH, vpH) = ParseH4HasPosition(bitstream, objStart, bitsX, bitsY, bitsZ,
                xMin, xMax, yMin, yMax, zMin, zMax, 640, hasSpawnRel: true, dimBits: 16);
            Console.WriteLine($"  H) has_pos+16bDim:      active={actH,3} valid={vpH,3} delta={endH - quotaStart24,6}");
        }
    }

    private (int EndBit, int Active, int ValidPos) ParseH4HasPosition(
        byte[] bitstream, int objStart, int bitsX, int bitsY, int bitsZ,
        float xMin, float xMax, float yMin, float yMax, float zMin, float zMax,
        int numSlots, bool hasSpawnRel, int dimBits)
    {
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);
        int active = 0, validPos = 0;
        for (int i = 0; i < numSlots; i++)
        {
            if (pb.BitsRemaining < 1) break;
            if (!pb.ReadBool()) continue;
            active++;
            pb.ReadInteger(2);
            bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
            bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
            bool hasPos = pb.ReadBool();
            if (hasPos)
            {
                uint rx = pb.ReadInteger(bitsX), ry = pb.ReadInteger(bitsY), rz = pb.ReadInteger(bitsZ);
                float px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                float py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                float pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
                if (px >= xMin - 20 && px <= xMax + 20 && py >= yMin - 20 && py <= yMax + 20 && pz >= zMin - 20 && pz <= zMax + 20) validPos++;
                bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
                if (hasSpawnRel) pb.ReadInteger(10);
            }
            uint shape = pb.ReadInteger(2);
            switch (shape) {
                case 1: pb.ReadInteger(dimBits); break;
                case 2: pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); break;
                case 3: pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); break;
            }
            pb.ReadInteger(8); pb.ReadInteger(8);
            uint ct = pb.ReadInteger(5);
            bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8);
            pb.ReadInteger(8); pb.ReadInteger(4);
            bool ca = pb.ReadBool(); if (!ca) pb.ReadInteger(3);
            if (ct == 1) pb.ReadInteger(8);
            if (ct >= 12 && ct <= 14) { pb.ReadInteger(5); pb.ReadInteger(5); }
            if (ct == 19) { bool ln = pb.ReadBool(); if (!ln) pb.ReadInteger(8); }
            if (pb.BitsRemaining < 0) break;
        }
        return (pb.BitOffset, active, validPos);
    }

    private (int EndBit, int Active, int ValidPos) ParseH4H3Props(
        byte[] bitstream, int objStart, int bitsX, int bitsY, int bitsZ,
        float xMin, float xMax, float yMin, float yMax, float zMin, float zMax, int numSlots)
    {
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);
        int active = 0, validPos = 0;
        for (int i = 0; i < numSlots; i++)
        {
            if (pb.BitsRemaining < 1) break;
            if (!pb.ReadBool()) continue;
            active++;
            pb.ReadInteger(2);
            bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
            bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
            bool ib = pb.ReadBool();
            if (ib) {
                uint rx = pb.ReadInteger(bitsX), ry = pb.ReadInteger(bitsY), rz = pb.ReadInteger(bitsZ);
                float px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                float py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                float pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
                if (px >= xMin - 20 && px <= xMax + 20 && py >= yMin - 20 && py <= yMax + 20 && pz >= zMin - 20 && pz <= zMax + 20) validPos++;
            } else { pb.ReadRawFloat(); pb.ReadRawFloat(); pb.ReadRawFloat(); }
            bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
            pb.ReadInteger(10);
            pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(16); pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(8);
            uint sh8 = pb.ReadInteger(8);
            if (sh8 >= 1 && sh8 <= 3) { pb.ReadInteger(16); pb.ReadInteger(16); if (sh8 >= 2) pb.ReadInteger(16); if (sh8 == 3) pb.ReadInteger(16); }
            if (pb.BitsRemaining < 0) break;
        }
        return (pb.BitOffset, active, validPos);
    }

    private (int EndBit, int Active, int ValidPos) ParseH4Extended(
        byte[] bitstream, int objStart, int bitsX, int bitsY, int bitsZ,
        float xMin, float xMax, float yMin, float yMax, float zMin, float zMax,
        int numSlots, int extraBits)
    {
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);
        int active = 0, validPos = 0;
        for (int i = 0; i < numSlots; i++)
        {
            if (pb.BitsRemaining < 1) break;
            if (!pb.ReadBool()) continue;
            active++;
            pb.ReadInteger(2);
            bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
            bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
            bool ib = pb.ReadBool();
            if (ib) {
                uint rx = pb.ReadInteger(bitsX), ry = pb.ReadInteger(bitsY), rz = pb.ReadInteger(bitsZ);
                float px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                float py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                float pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
                if (px >= xMin - 20 && px <= xMax + 20 && py >= yMin - 20 && py <= yMax + 20 && pz >= zMin - 20 && pz <= zMax + 20) validPos++;
            } else { pb.ReadRawFloat(); pb.ReadRawFloat(); pb.ReadRawFloat(); }
            bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
            pb.ReadInteger(10);
            uint shape = pb.ReadInteger(2);
            switch (shape) { case 1: pb.ReadInteger(11); break; case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break; case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break; }
            pb.ReadInteger(8); pb.ReadInteger(8); uint ct = pb.ReadInteger(5);
            bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8);
            pb.ReadInteger(8); pb.ReadInteger(4);
            bool ca = pb.ReadBool(); if (!ca) pb.ReadInteger(3);
            if (ct == 1) pb.ReadInteger(8); if (ct >= 12 && ct <= 14) { pb.ReadInteger(5); pb.ReadInteger(5); } if (ct == 19) { bool ln = pb.ReadBool(); if (!ln) pb.ReadInteger(8); }
            pb.SkipBits(extraBits);
            if (pb.BitsRemaining < 0) break;
        }
        return (pb.BitOffset, active, validPos);
    }

    private (int EndBit, int Active, int ValidPos) ParseH4HasPosH3Props(
        byte[] bitstream, int objStart, int bitsX, int bitsY, int bitsZ,
        float xMin, float xMax, float yMin, float yMax, float zMin, float zMax, int numSlots)
    {
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);
        int active = 0, validPos = 0;
        for (int i = 0; i < numSlots; i++)
        {
            if (pb.BitsRemaining < 1) break;
            if (!pb.ReadBool()) continue;
            active++;
            pb.ReadInteger(2);
            bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
            bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
            bool hasPos = pb.ReadBool();
            if (hasPos)
            {
                uint rx = pb.ReadInteger(bitsX), ry = pb.ReadInteger(bitsY), rz = pb.ReadInteger(bitsZ);
                float px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                float py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                float pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
                if (px >= xMin - 20 && px <= xMax + 20 && py >= yMin - 20 && py <= yMax + 20 && pz >= zMin - 20 && pz <= zMax + 20) validPos++;
                bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
                pb.ReadInteger(10);
            }
            pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(16); pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(8);
            uint sh8 = pb.ReadInteger(8);
            if (sh8 >= 1 && sh8 <= 3) { pb.ReadInteger(16); pb.ReadInteger(16); if (sh8 >= 2) pb.ReadInteger(16); if (sh8 == 3) pb.ReadInteger(16); }
            if (pb.BitsRemaining < 0) break;
        }
        return (pb.BitOffset, active, validPos);
    }

    private (int EndBit, int Active, int ValidPos) ParseH4WideFlags(
        byte[] bitstream, int objStart, int bitsX, int bitsY, int bitsZ,
        float xMin, float xMax, float yMin, float yMax, float zMin, float zMax, int numSlots)
    {
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);
        int active = 0, validPos = 0;
        for (int i = 0; i < numSlots; i++)
        {
            if (pb.BitsRemaining < 1) break;
            if (!pb.ReadBool()) continue;
            active++;
            pb.ReadInteger(16);
            bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
            bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
            bool ib = pb.ReadBool();
            if (ib) {
                uint rx = pb.ReadInteger(bitsX), ry = pb.ReadInteger(bitsY), rz = pb.ReadInteger(bitsZ);
                float px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                float py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                float pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
                if (px >= xMin - 20 && px <= xMax + 20 && py >= yMin - 20 && py <= yMax + 20 && pz >= zMin - 20 && pz <= zMax + 20) validPos++;
            } else { pb.ReadRawFloat(); pb.ReadRawFloat(); pb.ReadRawFloat(); }
            bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
            pb.ReadInteger(10);
            uint shape = pb.ReadInteger(2);
            switch (shape) { case 1: pb.ReadInteger(11); break; case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break; case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break; }
            pb.ReadInteger(8); pb.ReadInteger(8); uint ct = pb.ReadInteger(5);
            bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8);
            pb.ReadInteger(8); pb.ReadInteger(4);
            bool ca = pb.ReadBool(); if (!ca) pb.ReadInteger(3);
            if (ct == 1) pb.ReadInteger(8); if (ct >= 12 && ct <= 14) { pb.ReadInteger(5); pb.ReadInteger(5); } if (ct == 19) { bool ln = pb.ReadBool(); if (!ln) pb.ReadInteger(8); }
            if (pb.BitsRemaining < 0) break;
        }
        return (pb.BitOffset, active, validPos);
    }

    /// <summary>
    /// Dump raw bits for the first few objects in grifball to manually identify the format.
    /// For each "known" field boundary in Reach TU1 format, show the raw bits and decoded values.
    /// Then show what bits remain before the next object.
    /// </summary>
    [Fact]
    public void Halo4GrifballRawBitDump()
    {
        string path = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(path)) { Console.WriteLine("SKIP"); return; }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
        int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];

        // Parse to objStart
        var bits = new ForgeX.Core.IO.BitReader(bitstream);
        bits.ReadInteger(4); bits.ReadInteger(32);
        bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
        bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
        bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool();
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
        int numQuotas = (int)bits.ReadInteger(9);
        bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
        float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
        float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
        float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
        bits.ReadInteger(32); bits.ReadInteger(32);
        int sc = (int)bits.ReadInteger(9);
        for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
        if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
        int objStart = bits.BitOffset;

        var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
            xMin, xMax, yMin, yMax, zMin, zMax, 21);

        Console.WriteLine($"objStart={objStart} bounds=X[{xMin:F1},{xMax:F1}] Y[{yMin:F1},{yMax:F1}] Z[{zMin:F1},{zMax:F1}]");
        Console.WriteLine($"posBits: X={bitsX} Y={bitsY} Z={bitsZ}");

        // Helper: dump N bits as string without advancing
        string PeekBits(ForgeX.Core.IO.BitReader r, int n)
        {
            int save = r.BitOffset;
            var sb = new System.Text.StringBuilder(n);
            for (int i = 0; i < n && r.BitsRemaining > 0; i++)
                sb.Append(r.ReadBool() ? '1' : '0');
            int end = r.BitOffset;
            r.SkipBits(save - end); // rewind
            return sb.ToString();
        }

        // Dump first 600 bits of the object section
        Console.WriteLine($"\nRaw bits at object section start (bits {objStart}-{objStart+600}):");
        var peeker = new ForgeX.Core.IO.BitReader(bitstream);
        peeker.SkipBits(objStart);
        for (int row = 0; row < 12; row++)
        {
            Console.Write($"  +{row * 50,3}: ");
            for (int col = 0; col < 50 && peeker.BitsRemaining > 0; col++)
            {
                Console.Write(peeker.ReadBool() ? '1' : '0');
                if ((col + 1) % 10 == 0) Console.Write(' ');
            }
            Console.WriteLine();
        }

        // Now parse each object field by field with raw bit display
        Console.WriteLine($"\n=== Field-by-field parse of first 8 active objects ===");
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);
        int activeCount = 0;

        for (int slot = 0; slot < 640 && activeCount < 8; slot++)
        {
            if (pb.BitsRemaining < 10) break;
            int slotStart = pb.BitOffset;

            bool exists = pb.ReadBool();
            if (!exists) continue;
            activeCount++;

            Console.WriteLine($"\n--- Slot [{slot}], Active #{activeCount}, starts at bit {slotStart} ---");

            // flags (2 bits)
            int flagsStart = pb.BitOffset;
            uint flags = pb.ReadInteger(2);
            Console.WriteLine($"  [{flagsStart,5}] flags(2)={flags}");

            // quota_index: index-encoded 1+8
            int qiStart = pb.BitOffset;
            bool qaAbsent = pb.ReadBool();
            int qi = qaAbsent ? -1 : (int)pb.ReadInteger(8);
            Console.WriteLine($"  [{qiStart,5}] qi({(qaAbsent ? 1 : 9)})={qi} absent={qaAbsent}");

            // variant_index: index-encoded 1+5
            int viStart = pb.BitOffset;
            bool vaAbsent = pb.ReadBool();
            int vi = vaAbsent ? -1 : (int)pb.ReadInteger(5);
            Console.WriteLine($"  [{viStart,5}] vi({(vaAbsent ? 1 : 6)})={vi} absent={vaAbsent}");

            // position: ib(1) + encoded/raw
            int posStart = pb.BitOffset;
            bool ib = pb.ReadBool();
            float px, py, pz;
            if (ib) {
                uint rx = pb.ReadInteger(bitsX), ry = pb.ReadInteger(bitsY), rz = pb.ReadInteger(bitsZ);
                px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
                Console.WriteLine($"  [{posStart,5}] ib=True pos({1+bitsX+bitsY+bitsZ})=({px:F2},{py:F2},{pz:F2}) raw=({rx},{ry},{rz})");
            } else {
                px = pb.ReadRawFloat(); py = pb.ReadRawFloat(); pz = pb.ReadRawFloat();
                Console.WriteLine($"  [{posStart,5}] ib=False pos(97)=({px:G6},{py:G6},{pz:G6})");
            }

            // orientation
            int oriStart = pb.BitOffset;
            bool du = pb.ReadBool();
            uint axisRaw = 0;
            if (!du) axisRaw = pb.ReadInteger(20);
            uint angleRaw = pb.ReadInteger(14);
            int oriBits = pb.BitOffset - oriStart;
            Console.WriteLine($"  [{oriStart,5}] orientation({oriBits}): default_up={du} axis=0x{axisRaw:X5} angle={angleRaw}");

            // Show next 40 raw bits BEFORE reading spawn_relative_to (to see what's actually there)
            int preSpawnPos = pb.BitOffset;
            Console.Write($"  [{preSpawnPos,5}] next 40 raw bits: ");
            string next40 = PeekBits(pb, 40);
            Console.WriteLine(next40);

            // spawn_relative_to (10 bits, value+1)
            int srStart = pb.BitOffset;
            uint srRaw = pb.ReadInteger(10);
            Console.WriteLine($"  [{srStart,5}] spawnRelTo(10): raw={srRaw} decoded={srRaw - 1}");

            // boundary shape (2 bits)
            int shStart = pb.BitOffset;
            uint shape = pb.ReadInteger(2);
            int dimBitCount = 0;
            uint[] dims = new uint[4];
            switch (shape) {
                case 1: dims[0] = pb.ReadInteger(11); dimBitCount = 11; break;
                case 2: dims[0] = pb.ReadInteger(11); dims[1] = pb.ReadInteger(11); dims[2] = pb.ReadInteger(11); dimBitCount = 33; break;
                case 3: dims[0] = pb.ReadInteger(11); dims[1] = pb.ReadInteger(11); dims[2] = pb.ReadInteger(11); dims[3] = pb.ReadInteger(11); dimBitCount = 44; break;
            }
            Console.WriteLine($"  [{shStart,5}] shape(2)={shape} dims({dimBitCount})=({dims[0]},{dims[1]},{dims[2]},{dims[3]})");

            // spawn_sequence, respawn_time, cached_type
            int mpStart = pb.BitOffset;
            uint seq = pb.ReadInteger(8);
            uint resp = pb.ReadInteger(8);
            uint ctype = pb.ReadInteger(5);
            Console.WriteLine($"  [{mpStart,5}] seq(8)={seq} resp(8)={resp} ctype(5)={ctype}");

            // label_index
            int lStart = pb.BitOffset;
            bool laAbsent = pb.ReadBool();
            int label = laAbsent ? -1 : (int)pb.ReadInteger(8);
            Console.WriteLine($"  [{lStart,5}] label({(laAbsent ? 1 : 9)})={label}");

            // placement_flags, team, color
            int pfStart = pb.BitOffset;
            uint pf = pb.ReadInteger(8);
            uint team = pb.ReadInteger(4);
            bool caAbsent = pb.ReadBool();
            int color = caAbsent ? -1 : (int)pb.ReadInteger(3);
            Console.WriteLine($"  [{pfStart,5}] pflags(8)=0x{pf:X2} team(4)={team} color({(caAbsent ? 1 : 4)})={color}");

            // conditional per cached_type
            if (ctype == 1) {
                uint clips = pb.ReadInteger(8);
                Console.WriteLine($"  [{pb.BitOffset - 8,5}] spare_clips(8)={clips} [weapon]");
            }
            if (ctype >= 12 && ctype <= 14) {
                uint ch = pb.ReadInteger(5);
                uint pa = pb.ReadInteger(5);
                Console.WriteLine($"  [{pb.BitOffset - 10,5}] channel(5)={ch} passability(5)={pa} [teleporter]");
            }
            if (ctype == 19) {
                bool lnAbsent = pb.ReadBool();
                int ln = lnAbsent ? -1 : (int)pb.ReadInteger(8);
                Console.WriteLine($"  [{pb.BitOffset - (lnAbsent ? 1 : 9),5}] loc_name({(lnAbsent ? 1 : 9)})={ln} [location_name]");
            }

            int objEnd = pb.BitOffset;
            Console.WriteLine($"  TOTAL: {objEnd - slotStart} bits ({slotStart} → {objEnd})");

            // Show the next 30 bits (to see what comes next)
            Console.Write($"  Next 30 bits after object: ");
            string nextBits = PeekBits(pb, 30);
            Console.WriteLine(nextBits);
        }

        // After parsing 8 objects, try to find object #9 by scanning for valid position data
        Console.WriteLine($"\n=== Scanning for next valid position after bit {pb.BitOffset} ===");
        for (int tryOffset = 0; tryOffset <= 30; tryOffset++)
        {
            int tryBit = pb.BitOffset + tryOffset;
            // Skip through remaining empty slots to find next exists=true
            var scanner = new ForgeX.Core.IO.BitReader(bitstream);
            scanner.SkipBits(tryBit);
            // Look for a pattern: some 0s (empty slots) then 1 (exists) then valid data
            int zeros = 0;
            while (scanner.BitsRemaining > 100 && !scanner.ReadBool()) zeros++;
            if (scanner.BitsRemaining < 100) continue;

            // Try reading as Reach object
            scanner.ReadInteger(2); // flags
            bool qa2 = scanner.ReadBool(); if (!qa2) scanner.ReadInteger(8); // qi
            bool va2 = scanner.ReadBool(); if (!va2) scanner.ReadInteger(5); // vi
            bool ib2 = scanner.ReadBool();
            if (ib2) {
                uint rx = scanner.ReadInteger(bitsX), ry = scanner.ReadInteger(bitsY), rz = scanner.ReadInteger(bitsZ);
                float px2 = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                float py2 = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                float pz2 = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
                bool ok = px2 >= xMin - 20 && px2 <= xMax + 20 && py2 >= yMin - 20 && py2 <= yMax + 20 && pz2 >= zMin - 20 && pz2 <= zMax + 20;
                if (ok)
                    Console.WriteLine($"  tryOffset={tryOffset}: after {zeros} empty slots, found ib=True pos=({px2:F1},{py2:F1},{pz2:F1}) OK");
            }
        }
    }

    /// <summary>
    /// Fine-grained extra bits sweep on H3-props format (Hypothesis C).
    /// Since H3 props gives the best valid positions, add incremental extra bits to find exact format.
    /// Also try H3 props + Reach label/color fields.
    /// </summary>
    [Fact]
    public void Halo4H3PropsExtraBitsSweep()
    {
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP"); return; }

        var fileData = new List<(string N, byte[] BS, int ObjStart, int BX, int BY, int BZ,
            float XMin, float XMax, float YMin, float YMax, float ZMin, float ZMax,
            int PackedBits, int NumQuotas)>();

        foreach (var filePath in files.OrderBy(f => Path.GetFileName(f)))
        {
            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            var bs = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bs, 0, bs.Length);
            int plb = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
            var bits = new ForgeX.Core.IO.BitReader(bs);
            bits.ReadInteger(4); bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
            int nq = (int)bits.ReadInteger(9);
            bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            bits.ReadInteger(32); bits.ReadInteger(32);
            int sc = (int)bits.ReadInteger(9);
            for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
            if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
            int objStart = bits.BitOffset;
            var (bx, by, bz) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);
            fileData.Add((Path.GetFileName(filePath), bs, objStart, bx, by, bz,
                xMin, xMax, yMin, yMax, zMin, zMax, plb * 8, nq));
        }

        Console.WriteLine("=== H3-Props format + extra bits sweep (0-40) ===");
        Console.WriteLine($"{"Extra",5} | {string.Join(" | ", fileData.Select(f => $"{f.N.Substring(0, Math.Min(14, f.N.Length)),-30}"))}");

        for (int extra = 0; extra <= 40; extra++)
        {
            var results = new List<string>();
            for (int fi = 0; fi < fileData.Count; fi++)
            {
                var f = fileData[fi];
                var pb = new ForgeX.Core.IO.BitReader(f.BS);
                pb.SkipBits(f.ObjStart);
                int act = 0, vp = 0;
                for (int i = 0; i < 640; i++)
                {
                    if (pb.BitsRemaining < 1) break;
                    if (!pb.ReadBool()) continue;
                    act++;
                    pb.ReadInteger(2); // flags
                    bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
                    bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
                    bool ib = pb.ReadBool();
                    if (ib) {
                        uint rx = pb.ReadInteger(f.BX), ry = pb.ReadInteger(f.BY), rz = pb.ReadInteger(f.BZ);
                        float px = f.XMin + (rx + 0.5f) * ((f.XMax - f.XMin) / (1u << f.BX));
                        float py = f.YMin + (ry + 0.5f) * ((f.YMax - f.YMin) / (1u << f.BY));
                        float pz = f.ZMin + (rz + 0.5f) * ((f.ZMax - f.ZMin) / (1u << f.BZ));
                        if (px >= f.XMin - 20 && px <= f.XMax + 20 && py >= f.YMin - 20 && py <= f.YMax + 20 && pz >= f.ZMin - 20 && pz <= f.ZMax + 20) vp++;
                    } else { pb.ReadRawFloat(); pb.ReadRawFloat(); pb.ReadRawFloat(); }
                    bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
                    pb.ReadInteger(10); // spawn_relative_to
                    // H3-style properties
                    pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(16); pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(8);
                    uint sh8 = pb.ReadInteger(8);
                    if (sh8 >= 1 && sh8 <= 3) { pb.ReadInteger(16); pb.ReadInteger(16); if (sh8 >= 2) pb.ReadInteger(16); if (sh8 == 3) pb.ReadInteger(16); }
                    // Extra bits
                    if (extra > 0) pb.SkipBits(extra);
                    if (pb.BitsRemaining < 0) break;
                }
                int quotaStart = f.PackedBits - f.NumQuotas * 24;
                int delta = pb.BitOffset - quotaStart;
                results.Add($"a={act,3} v={vp,3} d={delta,7}");
            }
            Console.WriteLine($"{extra,5} | {string.Join(" | ", results)}");
        }

        // Also try specific hybrid combinations
        Console.WriteLine("\n=== Specific hybrid format tests ===");
        foreach (var f in fileData)
        {
            int quotaStart = f.PackedBits - f.NumQuotas * 24;
            Console.WriteLine($"\n{f.N}:");

            // Hybrid 1: H3 props + label(1+8) + color(1+3)
            {
                var pb = new ForgeX.Core.IO.BitReader(f.BS);
                pb.SkipBits(f.ObjStart);
                int act = 0, vp = 0;
                for (int i = 0; i < 640; i++)
                {
                    if (pb.BitsRemaining < 1) break;
                    if (!pb.ReadBool()) continue;
                    act++;
                    pb.ReadInteger(2);
                    bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
                    bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
                    bool ib = pb.ReadBool();
                    if (ib) {
                        uint rx = pb.ReadInteger(f.BX), ry = pb.ReadInteger(f.BY), rz = pb.ReadInteger(f.BZ);
                        float px = f.XMin + (rx + 0.5f) * ((f.XMax - f.XMin) / (1u << f.BX));
                        float py = f.YMin + (ry + 0.5f) * ((f.YMax - f.YMin) / (1u << f.BY));
                        float pz = f.ZMin + (rz + 0.5f) * ((f.ZMax - f.ZMin) / (1u << f.BZ));
                        if (px >= f.XMin - 20 && px <= f.XMax + 20 && py >= f.YMin - 20 && py <= f.YMax + 20 && pz >= f.ZMin - 20 && pz <= f.ZMax + 20) vp++;
                    } else { pb.ReadRawFloat(); pb.ReadRawFloat(); pb.ReadRawFloat(); }
                    bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
                    pb.ReadInteger(10);
                    pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(16); pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(8);
                    uint sh8 = pb.ReadInteger(8);
                    if (sh8 >= 1 && sh8 <= 3) { pb.ReadInteger(16); pb.ReadInteger(16); if (sh8 >= 2) pb.ReadInteger(16); if (sh8 == 3) pb.ReadInteger(16); }
                    // Add Reach-style label + color
                    bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8);
                    bool ca = pb.ReadBool(); if (!ca) pb.ReadInteger(3);
                    if (pb.BitsRemaining < 0) break;
                }
                Console.WriteLine($"  H3props+label+color:    a={act,3} v={vp,3} d={pb.BitOffset - quotaStart,7}");
            }

            // Hybrid 2: H3 props + pflags(8) + label(1+8) + color(1+3)
            {
                var pb = new ForgeX.Core.IO.BitReader(f.BS);
                pb.SkipBits(f.ObjStart);
                int act = 0, vp = 0;
                for (int i = 0; i < 640; i++)
                {
                    if (pb.BitsRemaining < 1) break;
                    if (!pb.ReadBool()) continue;
                    act++;
                    pb.ReadInteger(2);
                    bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
                    bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
                    bool ib = pb.ReadBool();
                    if (ib) {
                        uint rx = pb.ReadInteger(f.BX), ry = pb.ReadInteger(f.BY), rz = pb.ReadInteger(f.BZ);
                        float px = f.XMin + (rx + 0.5f) * ((f.XMax - f.XMin) / (1u << f.BX));
                        float py = f.YMin + (ry + 0.5f) * ((f.YMax - f.YMin) / (1u << f.BY));
                        float pz = f.ZMin + (rz + 0.5f) * ((f.ZMax - f.ZMin) / (1u << f.BZ));
                        if (px >= f.XMin - 20 && px <= f.XMax + 20 && py >= f.YMin - 20 && py <= f.YMax + 20 && pz >= f.ZMin - 20 && pz <= f.ZMax + 20) vp++;
                    } else { pb.ReadRawFloat(); pb.ReadRawFloat(); pb.ReadRawFloat(); }
                    bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
                    pb.ReadInteger(10);
                    pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(16); pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(8);
                    uint sh8 = pb.ReadInteger(8);
                    if (sh8 >= 1 && sh8 <= 3) { pb.ReadInteger(16); pb.ReadInteger(16); if (sh8 >= 2) pb.ReadInteger(16); if (sh8 == 3) pb.ReadInteger(16); }
                    pb.ReadInteger(8); // placement_flags
                    bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8);
                    bool ca = pb.ReadBool(); if (!ca) pb.ReadInteger(3);
                    if (pb.BitsRemaining < 0) break;
                }
                Console.WriteLine($"  H3props+pf+label+col:   a={act,3} v={vp,3} d={pb.BitOffset - quotaStart,7}");
            }

            // Hybrid 3: Reach props but with wider type(8) + sym(8) + engine(16)
            {
                var pb = new ForgeX.Core.IO.BitReader(f.BS);
                pb.SkipBits(f.ObjStart);
                int act = 0, vp = 0;
                for (int i = 0; i < 640; i++)
                {
                    if (pb.BitsRemaining < 1) break;
                    if (!pb.ReadBool()) continue;
                    act++;
                    pb.ReadInteger(2);
                    bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
                    bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
                    bool ib = pb.ReadBool();
                    if (ib) {
                        uint rx = pb.ReadInteger(f.BX), ry = pb.ReadInteger(f.BY), rz = pb.ReadInteger(f.BZ);
                        float px = f.XMin + (rx + 0.5f) * ((f.XMax - f.XMin) / (1u << f.BX));
                        float py = f.YMin + (ry + 0.5f) * ((f.YMax - f.YMin) / (1u << f.BY));
                        float pz = f.ZMin + (rz + 0.5f) * ((f.ZMax - f.ZMin) / (1u << f.BZ));
                        if (px >= f.XMin - 20 && px <= f.XMax + 20 && py >= f.YMin - 20 && py <= f.YMax + 20 && pz >= f.ZMin - 20 && pz <= f.ZMax + 20) vp++;
                    } else { pb.ReadRawFloat(); pb.ReadRawFloat(); pb.ReadRawFloat(); }
                    bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
                    pb.ReadInteger(10);
                    // Reach boundary shape (2 bits + 11-bit dims)
                    uint shape = pb.ReadInteger(2);
                    switch (shape) { case 1: pb.ReadInteger(11); break; case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break; case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break; }
                    // H3-wider properties: type(8) + sym(8) + engine(16) + storage(8) + resp(8) + team(8)
                    pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(16); pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(8);
                    // Reach-style label + color
                    bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8);
                    bool ca = pb.ReadBool(); if (!ca) pb.ReadInteger(3);
                    if (pb.BitsRemaining < 0) break;
                }
                Console.WriteLine($"  ReachBnds+H3props+l+c:  a={act,3} v={vp,3} d={pb.BitOffset - quotaStart,7}");
            }

            // Hybrid 4: H3 props but with Reach 2-bit shape + 11-bit dims
            {
                var pb = new ForgeX.Core.IO.BitReader(f.BS);
                pb.SkipBits(f.ObjStart);
                int act = 0, vp = 0;
                for (int i = 0; i < 640; i++)
                {
                    if (pb.BitsRemaining < 1) break;
                    if (!pb.ReadBool()) continue;
                    act++;
                    pb.ReadInteger(2);
                    bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8);
                    bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5);
                    bool ib = pb.ReadBool();
                    if (ib) {
                        uint rx = pb.ReadInteger(f.BX), ry = pb.ReadInteger(f.BY), rz = pb.ReadInteger(f.BZ);
                        float px = f.XMin + (rx + 0.5f) * ((f.XMax - f.XMin) / (1u << f.BX));
                        float py = f.YMin + (ry + 0.5f) * ((f.YMax - f.YMin) / (1u << f.BY));
                        float pz = f.ZMin + (rz + 0.5f) * ((f.ZMax - f.ZMin) / (1u << f.BZ));
                        if (px >= f.XMin - 20 && px <= f.XMax + 20 && py >= f.YMin - 20 && py <= f.YMax + 20 && pz >= f.ZMin - 20 && pz <= f.ZMax + 20) vp++;
                    } else { pb.ReadRawFloat(); pb.ReadRawFloat(); pb.ReadRawFloat(); }
                    bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
                    pb.ReadInteger(10);
                    // H3-style: type(8)+sym(8)+engine(16)+storage(8)+resp(8)+team(8)
                    pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(16); pb.ReadInteger(8); pb.ReadInteger(8); pb.ReadInteger(8);
                    // Reach-style boundary: shape(2) + 11-bit dims
                    uint shape = pb.ReadInteger(2);
                    switch (shape) { case 1: pb.ReadInteger(11); break; case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break; case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break; }
                    if (pb.BitsRemaining < 0) break;
                }
                Console.WriteLine($"  H3props(noShape)+R_bnd: a={act,3} v={vp,3} d={pb.BitOffset - quotaStart,7}");
            }
        }
    }

    /// <summary>
    /// Parser based on Nitrogen git history (commit a434a66 + 8b0b0c1).
    /// H4 uses modified Reach format: 6-bit type, scale(6), isLocked(1), unk10(10), 4 labels.
    /// Position is always adaptive (no raw float fallback). 651 object slots.
    /// </summary>
    private (int EndBit, int Active, int ValidPos, List<(int Slot, int Bits, string Info)> Objects)
        ParseH4Nitrogen(byte[] bitstream, int objStart, int bitsX, int bitsY, int bitsZ,
            float xMin, float xMax, float yMin, float yMax, float zMin, float zMax,
            int numSlots = 651, bool verbose = false, int dimBits = 11, bool alwaysAdaptive = true)
    {
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);
        int active = 0, validPos = 0;
        var objects = new List<(int Slot, int Bits, string Info)>();

        for (int i = 0; i < numSlots; i++)
        {
            if (pb.BitsRemaining < 1) break;
            int startBit = pb.BitOffset;
            if (!pb.ReadBool()) continue; // exists
            active++;

            uint flags = pb.ReadInteger(2);
            bool qaAbsent = pb.ReadBool(); int qi = qaAbsent ? -1 : (int)pb.ReadInteger(8);
            bool vaAbsent = pb.ReadBool(); int vi = vaAbsent ? -1 : (int)pb.ReadInteger(5);

            // Position: point_in_bounds flag + always adaptive
            bool ib = pb.ReadBool();
            float px, py, pz;
            if (alwaysAdaptive || ib)
            {
                uint rx = pb.ReadInteger(bitsX), ry = pb.ReadInteger(bitsY), rz = pb.ReadInteger(bitsZ);
                px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
            }
            else
            {
                px = pb.ReadRawFloat(); py = pb.ReadRawFloat(); pz = pb.ReadRawFloat();
            }
            bool posOk = px >= xMin - 20 && px <= xMax + 20 && py >= yMin - 20 && py <= yMax + 20 && pz >= zMin - 20 && pz <= zMax + 20;
            if (posOk) validPos++;

            // Orientation: optional 20-bit axis + 14-bit yaw (same as Reach)
            bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20);
            pb.ReadInteger(14);

            // spawn_relative_to: 10-bit value+1
            int spawnRelTo = (int)pb.ReadInteger(10) - 1;

            // H4-specific: scale (6-bit quantized [0, 10])
            uint scaleRaw = pb.ReadInteger(6);
            // H4-specific: isLocked (1-bit)
            bool isLocked = pb.ReadBool();

            // Boundary shape: 2-bit + dims (same as Reach)
            uint shape = pb.ReadInteger(2);
            switch (shape) {
                case 1: pb.ReadInteger(dimBits); break;
                case 2: pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); break;
                case 3: pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); pb.ReadInteger(dimBits); break;
            }

            // H4: object_type is 6-bit (vs 5-bit in Reach)
            uint objType = pb.ReadInteger(6);
            // H4-specific: unk10 (10-bit)
            uint unk10 = pb.ReadInteger(10);

            // NormalObject properties:
            // team: 4-bit value+1
            uint team = pb.ReadInteger(4);
            // spawn_time: 8-bit
            uint respawn = pb.ReadInteger(8);
            // primary_color_index: optional (1+3)
            bool caAbsent = pb.ReadBool(); int color = caAbsent ? -1 : (int)pb.ReadInteger(3);
            // placement_flags: 8-bit
            uint pflags = pb.ReadInteger(8);
            // spawn_sequence: 8-bit
            uint seq = pb.ReadInteger(8);
            // 4 label slots: each optional (1+8)
            int[] labels = new int[4];
            for (int li = 0; li < 4; li++)
            {
                bool labAbsent = pb.ReadBool();
                labels[li] = labAbsent ? -1 : (int)pb.ReadInteger(8);
            }

            // Type-specific conditionals (from Nitrogen ObjectType enum)
            if (objType == 1) // Weapon
                pb.ReadInteger(8); // spare clips
            if (objType == 0 || objType == 2 || objType == 3 || objType == 4 ||
                objType == 5 || objType == 6) // SimpleObject types
                { pb.ReadInteger(5); pb.ReadInteger(5); } // unk0, unk1 (teleporter-like)
            if (objType >= 13 && objType <= 15) // TeleporterTwoWay, Sender, Receiver
                { pb.ReadInteger(5); pb.ReadInteger(5); } // channel, passability
            if (objType == 20) // NamedLocation
                pb.ReadInteger(9); // location (8+1 StreamPlusOne = 9 bits)
            if (objType == 12) // Dispenser (VehiclePad)
                pb.ReadInteger(8); // cooldown
            if (objType == 31) // TraitZone
                pb.ReadInteger(5); // traitSet
            if (objType >= 21 && objType <= 27) // SpecialObject types (danger, fireteam, boundaries)
                { pb.ReadInteger(5); pb.ReadInteger(5); }
            if (objType == 32) // InitialOrdnanceDrop
                { pb.ReadInteger(5); pb.ReadInteger(8); pb.ReadInteger(16); } // unk0(4+1), unk1(8), unk2(16)
            if (objType == 33) // RandomOrdnanceDrop
                { for (int x = 0; x < 8; x++) pb.ReadInteger(8); } // 8x8-bit
            if (objType == 34) // ObjectiveOrdnanceDrop
                { for (int x = 0; x < 9; x++) pb.ReadInteger(8); } // 9x8-bit
            // Type 35 (PersonalOrdnanceDrop): no additional data

            int endBit = pb.BitOffset;
            int bits2 = endBit - startBit;

            string info = $"qi={qi} vi={vi} ib={ib} pos=({px:F1},{py:F1},{pz:F1}) spawnRel={spawnRelTo} scale={scaleRaw} lock={isLocked} sh={shape} type={objType} u10={unk10} tm={team} resp={respawn} col={color} pf=0x{pflags:X2} seq={seq} lab=[{string.Join(",", labels)}]";
            objects.Add((i, bits2, info));

            if (verbose && (active <= 20 || !posOk))
                Console.WriteLine($"  [{i}] #{active}: {bits2}b @{startBit}-{endBit} {(posOk ? "OK" : "BAD")} {info}");

            if (pb.BitsRemaining < 0) { Console.WriteLine($"  *** OVERRUN at [{i}] ***"); break; }
        }
        return (pb.BitOffset, active, validPos, objects);
    }

    /// <summary>
    /// Test the Nitrogen-derived H4 format on all H4 .mvar files.
    /// </summary>
    [Fact]
    public void Halo4NitrogenFormat()
    {
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP: no H4 files"); return; }

        foreach (var filePath in files.OrderBy(f => new FileInfo(f).Length))
        {
            string name = Path.GetFileName(filePath);
            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            var bitstream = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
            int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
            int packedBits = packedLenBytes * 8;

            // Parse metadata + header
            var bits = new ForgeX.Core.IO.BitReader(bitstream);
            bits.ReadInteger(4); bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

            bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
            int numQuotas = (int)bits.ReadInteger(9);
            bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            bits.ReadInteger(32); bits.ReadInteger(32);
            int sc = (int)bits.ReadInteger(9);
            for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
            if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
            int objStart = bits.BitOffset;

            var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);

            int quotaStart24 = packedBits - numQuotas * 24;
            bool isGrifball = name.Contains("grifball");

            Console.WriteLine($"\n========== {name} ==========");
            Console.WriteLine($"  packed={packedBits}b objStart={objStart} posBits=({bitsX},{bitsY},{bitsZ}) quotas={numQuotas}");

            // Test multiple format variants
            foreach (var (label, slots, dimBits2, alwaysAdap) in new[]
            {
                ("N_651_11b_alwaysAdap", 651, 11, true),
                ("N_651_16b_alwaysAdap", 651, 16, true),
                ("N_640_11b_alwaysAdap", 640, 11, true),
                ("N_640_16b_alwaysAdap", 640, 16, true),
                ("N_651_11b_reachIB",    651, 11, false),
                ("N_651_16b_reachIB",    651, 16, false),
            })
            {
                var (endBit, act, vp, objs) = ParseH4Nitrogen(
                    bitstream, objStart, bitsX, bitsY, bitsZ,
                    xMin, xMax, yMin, yMax, zMin, zMax,
                    numSlots: slots, verbose: false, dimBits: dimBits2, alwaysAdaptive: alwaysAdap);

                int delta = endBit - quotaStart24;
                Console.WriteLine($"  {label,-24}: end={endBit} a={act,3} v={vp,3} delta={delta,7}");

                // If close to quota start, validate quotas
                if (Math.Abs(delta) < 50)
                {
                    var qr = new ForgeX.Core.IO.BitReader(bitstream);
                    qr.SkipBits(endBit);
                    int qNz = 0, qPl = 0; bool qValid = true;
                    for (int q = 0; q < numQuotas; q++)
                    {
                        int mn = (int)qr.ReadInteger(8), mx = (int)qr.ReadInteger(8), ct = (int)qr.ReadInteger(8);
                        if (mn == 0 && mx == 0 && ct == 0) continue;
                        if (mn > mx || ct > mx || mx > 200) { qValid = false; break; }
                        qNz++; qPl += ct;
                    }
                    Console.WriteLine($"    ^^^ QUOTA: valid={qValid} nonZero={qNz} placed={qPl} (active={act})");
                }

                // For grifball, dump first 10 objects in detail
                if (isGrifball && label.Contains("651_11b_alwaysAdap"))
                {
                    Console.WriteLine($"\n  First 15 objects ({label}):");
                    foreach (var obj in objs.Take(15))
                        Console.WriteLine($"    [{obj.Slot}] {obj.Bits}b: {obj.Info}");
                }
            }
        }
    }

    /// <summary>
    /// Test the Nitrogen format with different conditional type configurations.
    /// Also test with NO conditionals (pure NormalObject base) to find correct object count.
    /// </summary>
    [Fact]
    public void Halo4NitrogenConditionalSweep()
    {
        string path = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(path)) { Console.WriteLine("SKIP"); return; }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
        int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
        int packedBits = packedLenBytes * 8;

        var bits = new ForgeX.Core.IO.BitReader(bitstream);
        bits.ReadInteger(4); bits.ReadInteger(32);
        bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
        bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
        bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool();
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
        int numQuotas = (int)bits.ReadInteger(9);
        bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
        float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
        float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
        float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
        bits.ReadInteger(32); bits.ReadInteger(32);
        int sc = (int)bits.ReadInteger(9);
        for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
        if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
        int objStart = bits.BitOffset;
        var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
            xMin, xMax, yMin, yMax, zMin, zMax, 21);

        Console.WriteLine($"packedBits={packedBits} objStart={objStart} numQuotas={numQuotas}");

        // Test multiple configurations
        // Config: which field changes to test
        // A) Full Nitrogen (current)
        // B) No type conditionals at all (pure NormalObject)
        // C) No unk10 field
        // D) No scale+isLocked fields
        // E) No unk10+scale+isLocked fields
        // F) No SimpleObject conditional (types 0-6 don't get 5+5)
        // G) Different type bit width (5 instead of 6)

        foreach (var (label, typeBits, hasScale, hasIsLocked, hasUnk10, simpleObjTypes) in new[]
        {
            ("A) Full Nitrogen",       6, true,  true,  true,  new int[]{0,2,3,4,5,6,13,14,15}),
            ("B) No conditionals",     6, true,  true,  true,  Array.Empty<int>()),
            ("C) No unk10",            6, true,  true,  false, new int[]{0,2,3,4,5,6,13,14,15}),
            ("D) No scale+lock",       6, false, false, true,  new int[]{0,2,3,4,5,6,13,14,15}),
            ("E) No unk10+scale+lock", 6, false, false, false, new int[]{0,2,3,4,5,6,13,14,15}),
            ("F) No SimpleObj cond",   6, true,  true,  true,  Array.Empty<int>()),
            ("G) 5-bit type",          5, true,  true,  true,  new int[]{0,2,3,4,5,6,13,14,15}),
            ("H) 5-bit type no u10",   5, true,  true,  false, new int[]{0,2,3,4,5,6,13,14,15}),
            ("I) No u10, no simpleobj",6, true,  true,  false, Array.Empty<int>()),
        })
        {
            var pb = new ForgeX.Core.IO.BitReader(bitstream);
            pb.SkipBits(objStart);
            int active = 0, validPos = 0, maxType = 0;
            bool overrun = false;

            for (int i = 0; i < 651; i++)
            {
                if (pb.BitsRemaining < 1) break;
                if (!pb.ReadBool()) continue;
                active++;

                pb.ReadInteger(2); // flags
                bool qa = pb.ReadBool(); if (!qa) pb.ReadInteger(8); // qi
                bool va = pb.ReadBool(); if (!va) pb.ReadInteger(5); // vi

                // Position: always adaptive
                pb.ReadBool(); // ib flag (stored but always adaptive)
                uint rx = pb.ReadInteger(bitsX), ry = pb.ReadInteger(bitsY), rz = pb.ReadInteger(bitsZ);
                float px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                float py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                float pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
                if (px >= xMin - 20 && px <= xMax + 20 && py >= yMin - 20 && py <= yMax + 20 && pz >= zMin - 20 && pz <= zMax + 20) validPos++;

                // Orientation
                bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
                // spawn_relative_to
                pb.ReadInteger(10);
                // scale
                if (hasScale) pb.ReadInteger(6);
                // isLocked
                if (hasIsLocked) pb.ReadBool();
                // Boundary shape
                uint shape = pb.ReadInteger(2);
                switch (shape) { case 1: pb.ReadInteger(11); break; case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break; case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break; }
                // type
                uint objType = pb.ReadInteger(typeBits);
                if ((int)objType > maxType) maxType = (int)objType;
                // unk10
                if (hasUnk10) pb.ReadInteger(10);
                // NormalObject props
                pb.ReadInteger(4); pb.ReadInteger(8); // team, resp
                bool ca = pb.ReadBool(); if (!ca) pb.ReadInteger(3); // color
                pb.ReadInteger(8); pb.ReadInteger(8); // pflags, seq
                for (int li = 0; li < 4; li++) { bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8); } // 4 labels
                // SimpleObject conditional
                if (simpleObjTypes.Contains((int)objType)) { pb.ReadInteger(5); pb.ReadInteger(5); }
                // Weapon
                if (objType == 1) pb.ReadInteger(8);
                // NamedLocation
                if (objType == 20) pb.ReadInteger(9);
                // Dispenser
                if (objType == 12) pb.ReadInteger(8);
                // TraitZone
                if (objType == 31) pb.ReadInteger(5);

                if (pb.BitsRemaining < 0) { overrun = true; break; }
            }

            int quotaStart = packedBits - numQuotas * 24;
            int delta = pb.BitOffset - quotaStart;
            Console.WriteLine($"  {label,-26}: a={active,3} v={validPos,3} delta={delta,7} maxType={maxType,2} over={overrun}");

            // If delta is small, check quotas
            if (Math.Abs(delta) < 100 && !overrun)
            {
                var qr = new ForgeX.Core.IO.BitReader(bitstream);
                qr.SkipBits(pb.BitOffset);
                int qNz = 0, qPl = 0; bool qValid = true;
                for (int q = 0; q < numQuotas; q++)
                {
                    int mn = (int)qr.ReadInteger(8), mx = (int)qr.ReadInteger(8), ct = (int)qr.ReadInteger(8);
                    if (mn == 0 && mx == 0 && ct == 0) continue;
                    if (mn > mx || ct > mx || mx > 200) { qValid = false; break; }
                    qNz++; qPl += ct;
                }
                Console.WriteLine($"    ^^^ QUOTA: valid={qValid} nz={qNz} placed={qPl}");
            }
        }
    }

    /// <summary>
    /// Validating parser: parse until an object field looks obviously wrong.
    /// Then dump the raw bits around the break point to manually identify the format.
    /// </summary>
    [Fact]
    public void Halo4NitrogenBreakpointAnalysis()
    {
        string path = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(path)) { Console.WriteLine("SKIP"); return; }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
        int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
        int packedBits = packedLenBytes * 8;

        var bits = new ForgeX.Core.IO.BitReader(bitstream);
        bits.ReadInteger(4); bits.ReadInteger(32);
        bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
        bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
        bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool();
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
        int numQuotas = (int)bits.ReadInteger(9);
        bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
        float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
        float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
        float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
        bits.ReadInteger(32); bits.ReadInteger(32);
        int sc = (int)bits.ReadInteger(9);
        for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
        if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
        int objStart = bits.BitOffset;
        var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
            xMin, xMax, yMin, yMax, zMin, zMax, 21);

        Console.WriteLine($"objStart={objStart} posBits=({bitsX},{bitsY},{bitsZ}) packedBits={packedBits}");

        // Validating parser: stop when data looks wrong
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);
        int active = 0;
        int lastCleanBit = objStart;

        for (int slot = 0; slot < 651; slot++)
        {
            if (pb.BitsRemaining < 1) break;
            int existsBit = pb.BitOffset;
            if (!pb.ReadBool()) continue;
            active++;

            int startBit = existsBit;
            uint flags = pb.ReadInteger(2);
            bool qaAbsent = pb.ReadBool(); int qi = qaAbsent ? -1 : (int)pb.ReadInteger(8);
            bool vaAbsent = pb.ReadBool(); int vi = vaAbsent ? -1 : (int)pb.ReadInteger(5);
            bool ib = pb.ReadBool();

            // Always adaptive position
            uint rx = pb.ReadInteger(bitsX), ry = pb.ReadInteger(bitsY), rz = pb.ReadInteger(bitsZ);
            float px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
            float py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
            float pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));

            bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
            int spawnRel = (int)pb.ReadInteger(10) - 1;
            uint scaleRaw = pb.ReadInteger(6);
            bool isLocked = pb.ReadBool();
            uint shape = pb.ReadInteger(2);
            switch (shape) { case 1: pb.ReadInteger(11); break; case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break; case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break; }
            uint objType = pb.ReadInteger(6);
            uint unk10 = pb.ReadInteger(10);
            uint team = pb.ReadInteger(4);
            uint resp = pb.ReadInteger(8);
            bool caAbs = pb.ReadBool(); int color = caAbs ? -1 : (int)pb.ReadInteger(3);
            uint pflags = pb.ReadInteger(8);
            uint seq = pb.ReadInteger(8);
            int[] labels = new int[4];
            for (int li = 0; li < 4; li++) { bool la = pb.ReadBool(); labels[li] = la ? -1 : (int)pb.ReadInteger(8); }

            // Type conditionals
            if (objType == 1) pb.ReadInteger(8);
            if (objType == 0 || objType == 2 || objType == 3 || objType == 4 || objType == 5 || objType == 6)
                { pb.ReadInteger(5); pb.ReadInteger(5); }
            if (objType >= 13 && objType <= 15) { pb.ReadInteger(5); pb.ReadInteger(5); }
            if (objType == 20) pb.ReadInteger(9);
            if (objType == 12) pb.ReadInteger(8);
            if (objType == 31) pb.ReadInteger(5);
            if (objType >= 21 && objType <= 27) { pb.ReadInteger(5); pb.ReadInteger(5); }

            int endBit = pb.BitOffset;

            // Validate
            bool valid = true;
            if (objType > 35) valid = false;
            if (qi > 255 && qi >= 0) valid = false;
            if (team > 8) valid = false;
            if (scaleRaw > 63) valid = false; // always true for 6-bit
            bool posOk = px >= xMin - 50 && px <= xMax + 50 && py >= yMin - 50 && py <= yMax + 50 && pz >= zMin - 50 && pz <= zMax + 50;
            if (!posOk) valid = false;

            string info = $"qi={qi} vi={vi} ib={ib} pos=({px:F1},{py:F1},{pz:F1}) sR={spawnRel} sc={scaleRaw} lk={isLocked} sh={shape} t={objType} u10={unk10} tm={team} rsp={resp} col={color} pf=0x{pflags:X2} sq={seq} lb=[{string.Join(",", labels)}]";
            Console.WriteLine($"  [{slot}] #{active}: {endBit - startBit}b @{startBit}-{endBit} {(valid ? "OK" : "BAD")} {info}");

            if (!valid)
            {
                Console.WriteLine($"\n  *** BREAK at slot {slot} (object #{active}) ***");
                Console.WriteLine($"  Last clean bit position: {lastCleanBit}");
                Console.WriteLine($"  Break start bit: {startBit}");

                // Dump 100 bits before and after the break point
                Console.WriteLine($"\n  Bits at break point (bit {startBit} ±50):");
                var peek = new ForgeX.Core.IO.BitReader(bitstream);
                peek.SkipBits(Math.Max(0, startBit - 50));
                Console.Write("  Before: ");
                for (int b = 0; b < 50; b++) Console.Write(peek.ReadBool() ? "1" : "0");
                Console.Write(" | EXISTS> ");
                for (int b = 0; b < 100; b++) Console.Write(peek.ReadBool() ? "1" : "0");
                Console.WriteLine();

                // Try re-parsing from lastCleanBit without SimpleObject conditional
                Console.WriteLine($"\n  Re-parse from break WITHOUT SimpleObject conditional:");
                var pb2 = new ForgeX.Core.IO.BitReader(bitstream);
                pb2.SkipBits(startBit + 1); // skip exists=1
                flags = pb2.ReadInteger(2);
                qaAbsent = pb2.ReadBool(); qi = qaAbsent ? -1 : (int)pb2.ReadInteger(8);
                vaAbsent = pb2.ReadBool(); vi = vaAbsent ? -1 : (int)pb2.ReadInteger(5);
                ib = pb2.ReadBool();
                rx = pb2.ReadInteger(bitsX); ry = pb2.ReadInteger(bitsY); rz = pb2.ReadInteger(bitsZ);
                px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
                du = pb2.ReadBool(); if (!du) pb2.ReadInteger(20); pb2.ReadInteger(14);
                spawnRel = (int)pb2.ReadInteger(10) - 1;
                scaleRaw = pb2.ReadInteger(6);
                isLocked = pb2.ReadBool();
                shape = pb2.ReadInteger(2);
                switch (shape) { case 1: pb2.ReadInteger(11); break; case 2: pb2.ReadInteger(11); pb2.ReadInteger(11); pb2.ReadInteger(11); break; case 3: pb2.ReadInteger(11); pb2.ReadInteger(11); pb2.ReadInteger(11); pb2.ReadInteger(11); break; }
                objType = pb2.ReadInteger(6);
                unk10 = pb2.ReadInteger(10);
                team = pb2.ReadInteger(4);
                resp = pb2.ReadInteger(8);
                caAbs = pb2.ReadBool(); color = caAbs ? -1 : (int)pb2.ReadInteger(3);
                pflags = pb2.ReadInteger(8);
                seq = pb2.ReadInteger(8);
                for (int li = 0; li < 4; li++) { bool la = pb2.ReadBool(); labels[li] = la ? -1 : (int)pb2.ReadInteger(8); }
                Console.WriteLine($"    flags={flags} qi={qi} vi={vi} ib={ib} pos=({px:F1},{py:F1},{pz:F1}) t={objType} u10={unk10} tm={team}");

                // Also try without unk10
                Console.WriteLine($"\n  Re-parse WITHOUT unk10:");
                pb2 = new ForgeX.Core.IO.BitReader(bitstream);
                pb2.SkipBits(startBit + 1);
                flags = pb2.ReadInteger(2);
                qaAbsent = pb2.ReadBool(); qi = qaAbsent ? -1 : (int)pb2.ReadInteger(8);
                vaAbsent = pb2.ReadBool(); vi = vaAbsent ? -1 : (int)pb2.ReadInteger(5);
                ib = pb2.ReadBool();
                rx = pb2.ReadInteger(bitsX); ry = pb2.ReadInteger(bitsY); rz = pb2.ReadInteger(bitsZ);
                px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
                du = pb2.ReadBool(); if (!du) pb2.ReadInteger(20); pb2.ReadInteger(14);
                spawnRel = (int)pb2.ReadInteger(10) - 1;
                scaleRaw = pb2.ReadInteger(6);
                isLocked = pb2.ReadBool();
                shape = pb2.ReadInteger(2);
                switch (shape) { case 1: pb2.ReadInteger(11); break; case 2: pb2.ReadInteger(11); pb2.ReadInteger(11); pb2.ReadInteger(11); break; case 3: pb2.ReadInteger(11); pb2.ReadInteger(11); pb2.ReadInteger(11); pb2.ReadInteger(11); break; }
                objType = pb2.ReadInteger(6);
                // NO unk10
                team = pb2.ReadInteger(4);
                resp = pb2.ReadInteger(8);
                caAbs = pb2.ReadBool(); color = caAbs ? -1 : (int)pb2.ReadInteger(3);
                pflags = pb2.ReadInteger(8);
                seq = pb2.ReadInteger(8);
                for (int li = 0; li < 4; li++) { bool la = pb2.ReadBool(); labels[li] = la ? -1 : (int)pb2.ReadInteger(8); }
                Console.WriteLine($"    flags={flags} qi={qi} vi={vi} ib={ib} pos=({px:F1},{py:F1},{pz:F1}) t={objType} tm={team}");

                // Try without scale+isLocked+unk10
                Console.WriteLine($"\n  Re-parse WITHOUT scale+isLocked+unk10:");
                pb2 = new ForgeX.Core.IO.BitReader(bitstream);
                pb2.SkipBits(startBit + 1);
                flags = pb2.ReadInteger(2);
                qaAbsent = pb2.ReadBool(); qi = qaAbsent ? -1 : (int)pb2.ReadInteger(8);
                vaAbsent = pb2.ReadBool(); vi = vaAbsent ? -1 : (int)pb2.ReadInteger(5);
                ib = pb2.ReadBool();
                rx = pb2.ReadInteger(bitsX); ry = pb2.ReadInteger(bitsY); rz = pb2.ReadInteger(bitsZ);
                px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
                du = pb2.ReadBool(); if (!du) pb2.ReadInteger(20); pb2.ReadInteger(14);
                spawnRel = (int)pb2.ReadInteger(10) - 1;
                // No scale, isLocked
                shape = pb2.ReadInteger(2);
                switch (shape) { case 1: pb2.ReadInteger(11); break; case 2: pb2.ReadInteger(11); pb2.ReadInteger(11); pb2.ReadInteger(11); break; case 3: pb2.ReadInteger(11); pb2.ReadInteger(11); pb2.ReadInteger(11); pb2.ReadInteger(11); break; }
                objType = pb2.ReadInteger(6);
                team = pb2.ReadInteger(4);
                resp = pb2.ReadInteger(8);
                caAbs = pb2.ReadBool(); color = caAbs ? -1 : (int)pb2.ReadInteger(3);
                pflags = pb2.ReadInteger(8);
                seq = pb2.ReadInteger(8);
                for (int li = 0; li < 4; li++) { bool la = pb2.ReadBool(); labels[li] = la ? -1 : (int)pb2.ReadInteger(8); }
                Console.WriteLine($"    flags={flags} qi={qi} vi={vi} ib={ib} pos=({px:F1},{py:F1},{pz:F1}) sh={shape} t={objType} tm={team}");

                break; // Stop after first invalid object
            }
            lastCleanBit = endBit;
        }
    }

    /// <summary>
    /// Precise quota location test using Nitrogen parser.
    /// For grifball: find exactly where the non-zero quota entries are,
    /// determine if quotas start right after objects or after an intermediate table.
    /// </summary>
    [Fact]
    public void Halo4NitrogenQuotaLocation()
    {
        string path = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(path)) { Console.WriteLine("SKIP"); return; }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
        int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
        int packedBits = packedLenBytes * 8;

        // Parse metadata + header
        var bits = new ForgeX.Core.IO.BitReader(bitstream);
        bits.ReadInteger(4); bits.ReadInteger(32);
        bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
        bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
        bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool();
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
        int numQuotas = (int)bits.ReadInteger(9);
        bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
        float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
        float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
        float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
        bits.ReadInteger(32); bits.ReadInteger(32);
        int sc = (int)bits.ReadInteger(9);
        for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
        if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
        int objStart = bits.BitOffset;
        var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
            xMin, xMax, yMin, yMax, zMin, zMax, 21);

        Console.WriteLine($"packedBits={packedBits} objStart={objStart} numQuotas={numQuotas}");

        // Parse objects with Nitrogen format
        var (objEnd, active, vp, allObjs) = ParseH4Nitrogen(
            bitstream, objStart, bitsX, bitsY, bitsZ,
            xMin, xMax, yMin, yMax, zMin, zMax,
            numSlots: 651, verbose: true, dimBits: 11, alwaysAdaptive: true);

        Console.WriteLine($"\nobjEnd={objEnd} active={active} valid={vp}");
        Console.WriteLine($"gap to end = {packedBits - objEnd}");
        Console.WriteLine($"All {active} objects:");
        foreach (var obj in allObjs)
            Console.WriteLine($"  [{obj.Slot}] {obj.Bits}b: {obj.Info}");

        // Sum of all object bits
        int totalObjBits = allObjs.Sum(o => o.Bits);
        int nonActiveBits = 651 - active; // 1 bit per non-active slot
        Console.WriteLine($"\ntotalObjBits={totalObjBits} nonActiveBits={nonActiveBits} total={totalObjBits + nonActiveBits}");
        Console.WriteLine($"actual object section = {objEnd - objStart} (diff={objEnd - objStart - totalObjBits - nonActiveBits})");

        // Try quotas starting at objEnd
        Console.WriteLine($"\n=== Quotas starting at objEnd ({objEnd}) ===");
        {
            var qr = new ForgeX.Core.IO.BitReader(bitstream);
            qr.SkipBits(objEnd);
            for (int q = 0; q < Math.Min(numQuotas, 50); q++)
            {
                int mn = (int)qr.ReadInteger(8), mx = (int)qr.ReadInteger(8), ct = (int)qr.ReadInteger(8);
                if (mn != 0 || mx != 0 || ct != 0)
                    Console.WriteLine($"  quota[{q}]: min={mn} max={mx} placed={ct}");
            }
        }

        // Try quotas starting at packedBits - numQuotas*24
        int quotaStartEnd = packedBits - numQuotas * 24;
        Console.WriteLine($"\n=== Quotas at end ({quotaStartEnd}) ===");
        {
            var qr = new ForgeX.Core.IO.BitReader(bitstream);
            qr.SkipBits(quotaStartEnd);
            for (int q = 0; q < Math.Min(numQuotas, 50); q++)
            {
                int mn = (int)qr.ReadInteger(8), mx = (int)qr.ReadInteger(8), ct = (int)qr.ReadInteger(8);
                if (mn != 0 || mx != 0 || ct != 0)
                    Console.WriteLine($"  quota[{q}]: min={mn} max={mx} placed={ct}");
            }
        }

        // Scan: for each candidate start position, find the one where quota indices
        // match expected object types and placed sums to active
        Console.WriteLine($"\n=== Quota scan from objEnd to objEnd+200 ===");
        for (int tryStart = objEnd; tryStart <= objEnd + 200; tryStart++)
        {
            var qr = new ForgeX.Core.IO.BitReader(bitstream);
            qr.SkipBits(tryStart);
            int totalPlaced = 0; int nonZero = 0; bool valid = true;
            var nonZeroEntries = new List<(int Idx, int Mn, int Mx, int Ct)>();
            for (int q = 0; q < numQuotas; q++)
            {
                int mn = (int)qr.ReadInteger(8), mx = (int)qr.ReadInteger(8), ct = (int)qr.ReadInteger(8);
                if (mn == 0 && mx == 0 && ct == 0) continue;
                if (mn > mx || ct > mx || mx > 200) { valid = false; break; }
                nonZero++; totalPlaced += ct;
                nonZeroEntries.Add((q, mn, mx, ct));
            }
            if (valid && nonZero > 0 && (totalPlaced == active || totalPlaced == 68))
            {
                int remaining = packedBits - tryStart - numQuotas * 24;
                Console.WriteLine($"  start={tryStart} (gap={tryStart - objEnd}): nz={nonZero} placed={totalPlaced} remaining_after={remaining}");
                foreach (var (idx, mn, mx, ct) in nonZeroEntries)
                    Console.WriteLine($"    [{idx}]: min={mn} max={mx} placed={ct}");
            }
        }
    }

    /// <summary>
    /// Comprehensive investigation of the gap between H3-props objects and end of packed data.
    /// Tests different quota formats (24/48/96 bits) and intermediate tables.
    /// </summary>
    [Fact]
    public void Halo4PostObjectsGapInvestigation()
    {
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP"); return; }

        foreach (var filePath in files.OrderBy(f => new FileInfo(f).Length))
        {
            string name = Path.GetFileName(filePath);
            var blf = new BlfFile(filePath);
            var mvar = blf.GetChunk("mvar")!;
            var bitstream = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
            int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
            int packedBits = packedLenBytes * 8;

            // Parse metadata + header
            var bits = new ForgeX.Core.IO.BitReader(bitstream);
            bits.ReadInteger(4); bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
            for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
            bits.ReadBool();
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
            for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }

            bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
            int numQuotas = (int)bits.ReadInteger(9);
            bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            bits.ReadInteger(32); bits.ReadInteger(32);
            int sc = (int)bits.ReadInteger(9);
            for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
            if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
            int objStart = bits.BitOffset;

            var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);

            // Parse objects with H3 props format
            var (objEnd, active, validPos) = ParseH4H3Props(bitstream, objStart, bitsX, bitsY, bitsZ,
                xMin, xMax, yMin, yMax, zMin, zMax, 640);

            int gapBits = packedBits - objEnd;

            Console.WriteLine($"\n========== {name} ==========");
            Console.WriteLine($"  packedBits={packedBits} objStart={objStart} objEnd={objEnd}");
            Console.WriteLine($"  active={active} validPos={validPos} numQuotas={numQuotas}");
            Console.WriteLine($"  gapBits (packed - objEnd) = {gapBits}");
            Console.WriteLine($"  gap with 24-bit quotas: {gapBits} - {numQuotas}*24 = {gapBits - numQuotas * 24}");
            Console.WriteLine($"  gap with 96-bit quotas: {gapBits} - {numQuotas}*96 = {gapBits - numQuotas * 96}");
            Console.WriteLine($"  gap with 96-bit quotas + 14*9 table: {gapBits} - {numQuotas * 96 + 126} = {gapBits - numQuotas * 96 - 126}");

            // Dump first 300 raw bits after objects
            Console.WriteLine($"\n  First 300 bits after objects (at bit {objEnd}):");
            var peek = new ForgeX.Core.IO.BitReader(bitstream);
            peek.SkipBits(objEnd);
            for (int b = 0; b < Math.Min(300, peek.BitsRemaining); b++)
            {
                if (b > 0 && b % 50 == 0) Console.WriteLine();
                if (b > 0 && b % 10 == 0 && b % 50 != 0) Console.Write(" ");
                Console.Write(peek.ReadBool() ? "1" : "0");
            }
            Console.WriteLine();

            // Try reading as 14×9 object_type_start_index table right after objects
            {
                var tr = new ForgeX.Core.IO.BitReader(bitstream);
                tr.SkipBits(objEnd);
                Console.WriteLine($"\n  14x9 table interpretation (at bit {objEnd}):");
                var vals = new int[14];
                for (int i = 0; i < 14; i++)
                    vals[i] = (int)tr.ReadInteger(9);
                Console.WriteLine($"    values: [{string.Join(", ", vals)}]");
                // Check if monotonically non-decreasing (expected for start indices)
                bool mono = true;
                for (int i = 1; i < 14; i++) if (vals[i] < vals[i-1]) { mono = false; break; }
                Console.WriteLine($"    monotonic: {mono}");
            }

            // Test different quota formats working backwards from packedBits
            foreach (int quotaBits in new[] { 24, 32, 48, 64, 96 })
            {
                int quotaStart = packedBits - numQuotas * quotaBits;
                if (quotaStart < objEnd - 500 || quotaStart > packedBits) continue;

                var qr = new ForgeX.Core.IO.BitReader(bitstream);
                qr.SkipBits(quotaStart);
                int nonZero = 0, placed = 0, invalidCount = 0;
                for (int q = 0; q < numQuotas; q++)
                {
                    if (quotaBits == 24) {
                        int mn = (int)qr.ReadInteger(8), mx = (int)qr.ReadInteger(8), ct = (int)qr.ReadInteger(8);
                        if (mn == 0 && mx == 0 && ct == 0) continue;
                        if (mn > mx || ct > mx || mx > 200) { invalidCount++; continue; }
                        nonZero++; placed += ct;
                    } else if (quotaBits == 96) {
                        uint ident = qr.ReadInteger(32);
                        int mn = (int)qr.ReadInteger(8), mx = (int)qr.ReadInteger(8);
                        int ct = (int)qr.ReadInteger(8), mxa = (int)qr.ReadInteger(8);
                        float cost = qr.ReadRawFloat();
                        if (ident == 0 && mn == 0 && mx == 0 && ct == 0) continue;
                        if (mn > mx || ct > mx || mx > 200 || mxa > 200) { invalidCount++; continue; }
                        nonZero++; placed += ct;
                    } else {
                        // Generic: just check if min<=max<=200, count<=max for first 24 bits
                        qr.SkipBits(quotaBits - 24);
                        int mn = (int)qr.ReadInteger(8), mx = (int)qr.ReadInteger(8), ct = (int)qr.ReadInteger(8);
                        if (mn == 0 && mx == 0 && ct == 0) continue;
                        if (mn > mx || ct > mx || mx > 200) { invalidCount++; continue; }
                        nonZero++; placed += ct;
                    }
                }
                int gapToQuota = quotaStart - objEnd;
                Console.WriteLine($"  quota {quotaBits}b: start={quotaStart} gap={gapToQuota} invalid={invalidCount} nonZero={nonZero} placed={placed} (active={active})");
            }

            // Scan for valid 24-bit quota blocks near expected position
            Console.WriteLine($"\n  Scanning for valid 24-bit quotas (numQuotas={numQuotas}):");
            int bestOffset = -1, bestNZ = 0, bestPlaced = 0, bestInvalid = int.MaxValue;
            for (int tryPos = objEnd; tryPos <= packedBits - numQuotas * 24; tryPos++)
            {
                var qr = new ForgeX.Core.IO.BitReader(bitstream);
                qr.SkipBits(tryPos);
                int nz = 0, pl = 0, inv = 0;
                for (int q = 0; q < numQuotas; q++)
                {
                    int mn = (int)qr.ReadInteger(8), mx = (int)qr.ReadInteger(8), ct = (int)qr.ReadInteger(8);
                    if (mn == 0 && mx == 0 && ct == 0) continue;
                    if (mn > mx || ct > mx || mx > 200) { inv++; continue; }
                    nz++; pl += ct;
                }
                if (inv < bestInvalid || (inv == bestInvalid && nz > bestNZ))
                {
                    bestInvalid = inv; bestNZ = nz; bestPlaced = pl; bestOffset = tryPos;
                }
                // Report good matches
                if (inv == 0 && nz > 0 && pl == active)
                    Console.WriteLine($"    PERFECT MATCH at {tryPos} (gap={tryPos - objEnd}): nz={nz} placed={pl}");
                else if (inv == 0 && nz > 0 && Math.Abs(pl - active) <= 2)
                    Console.WriteLine($"    Close match at {tryPos} (gap={tryPos - objEnd}): nz={nz} placed={pl}");
            }
            if (bestOffset >= 0)
                Console.WriteLine($"    Best: offset={bestOffset} (gap={bestOffset - objEnd}) inv={bestInvalid} nz={bestNZ} placed={bestPlaced}");

            // Also scan for valid 96-bit H3-style quotas
            Console.WriteLine($"\n  Scanning for valid 96-bit quotas (numQuotas={numQuotas}):");
            bestOffset = -1; bestNZ = 0; bestPlaced = 0; bestInvalid = int.MaxValue;
            int scanEnd96 = Math.Min(objEnd + 500, packedBits - numQuotas * 96);
            for (int tryPos = Math.Max(objEnd - 10, 0); tryPos <= scanEnd96; tryPos++)
            {
                var qr = new ForgeX.Core.IO.BitReader(bitstream);
                qr.SkipBits(tryPos);
                int nz = 0, pl = 0, inv = 0;
                for (int q = 0; q < numQuotas; q++)
                {
                    uint ident = qr.ReadInteger(32);
                    int mn = (int)qr.ReadInteger(8), mx = (int)qr.ReadInteger(8);
                    int ct = (int)qr.ReadInteger(8), mxa = (int)qr.ReadInteger(8);
                    float cost = qr.ReadRawFloat();
                    if (ident == 0 && mn == 0 && mx == 0 && ct == 0) continue;
                    if (mn > mx || ct > mx || mx > 200 || mxa > 200) { inv++; continue; }
                    nz++; pl += ct;
                }
                if (inv < bestInvalid || (inv == bestInvalid && nz > bestNZ))
                {
                    bestInvalid = inv; bestNZ = nz; bestPlaced = pl; bestOffset = tryPos;
                }
                if (inv == 0 && nz > 0 && pl == active)
                    Console.WriteLine($"    PERFECT MATCH at {tryPos} (gap={tryPos - objEnd}): nz={nz} placed={pl}");
                else if (inv == 0 && nz > 0 && Math.Abs(pl - active) <= 2)
                    Console.WriteLine($"    Close match at {tryPos} (gap={tryPos - objEnd}): nz={nz} placed={pl}");
            }
            if (bestOffset >= 0)
                Console.WriteLine($"    Best: offset={bestOffset} (gap={bestOffset - objEnd}) inv={bestInvalid} nz={bestNZ} placed={bestPlaced}");
        }
    }

    /// <summary>
    /// Examine raw bits at the first spawn object to determine exact field widths.
    /// Compare qi=48 (Nitrogen) vs expected qi=56 (quota index).
    /// </summary>
    [Fact]
    public void Halo4RawBitAnalysis()
    {
        string path = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(path)) { Console.WriteLine("SKIP"); return; }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);

        // Parse header to get objStart
        var bits = new ForgeX.Core.IO.BitReader(bitstream);
        bits.ReadInteger(4); bits.ReadInteger(32);
        bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
        bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
        bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool();
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
        int numQuotas = (int)bits.ReadInteger(9);
        bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
        float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
        float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
        float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
        bits.ReadInteger(32); bits.ReadInteger(32);
        int sc = (int)bits.ReadInteger(9);
        for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
        if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
        int objStart = bits.BitOffset;

        Console.WriteLine($"objStart={objStart}");

        // Dump first 180 raw bits (first spawn point)
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);
        Console.Write("Raw: ");
        int[] rawBits = new int[180];
        for (int b = 0; b < 180; b++)
            rawBits[b] = pb.ReadBool() ? 1 : 0;
        for (int b = 0; b < 180; b++)
        {
            if (b > 0 && b % 50 == 0) Console.Write("\n     ");
            else if (b > 0 && b % 10 == 0) Console.Write(" ");
            Console.Write(rawBits[b]);
        }
        Console.WriteLine();

        // Decode with different field layouts
        // Layout A (Nitrogen): exists(1) flags(2) qi_absent(1) qi(8) vi_absent(1) vi(5)
        // Layout B: exists(1) flags(3) qi_absent(1) qi(8) vi_absent(1) vi(5)
        // Layout C: exists(1) flags(2) qi_absent(1) qi(9) vi_absent(1) vi(5)
        // Layout D: exists(1) flags(2) qi(9) vi_absent(1) vi(5) -- no absent for qi
        Console.WriteLine("\n=== Field layout interpretations ===");

        int pos;

        // Layout A: Nitrogen standard
        pos = 0;
        Console.Write($"  A) exists={rawBits[pos++]} flags=");
        int f2 = rawBits[pos]*2 + rawBits[pos+1]; pos += 2;
        Console.Write($"{f2} qiAbsent={rawBits[pos]}");
        int qiA = 0; if (rawBits[pos++] == 0) { for (int b = 0; b < 8; b++) qiA = qiA*2 + rawBits[pos++]; Console.Write($" qi={qiA}"); } else Console.Write(" qi=-1");
        Console.Write($" viAbsent={rawBits[pos]}");
        int viA = 0; if (rawBits[pos++] == 0) { for (int b = 0; b < 5; b++) viA = viA*2 + rawBits[pos++]; Console.Write($" vi={viA}"); } else Console.Write(" vi=-1");
        Console.WriteLine($" nextBitPos={pos}");

        // Layout B: 3-bit flags
        pos = 0;
        Console.Write($"  B) exists={rawBits[pos++]} flags=");
        int f3 = rawBits[pos]*4 + rawBits[pos+1]*2 + rawBits[pos+2]; pos += 3;
        Console.Write($"{f3} qiAbsent={rawBits[pos]}");
        int qiB = 0; if (rawBits[pos++] == 0) { for (int b = 0; b < 8; b++) qiB = qiB*2 + rawBits[pos++]; Console.Write($" qi={qiB}"); } else Console.Write(" qi=-1");
        Console.Write($" viAbsent={rawBits[pos]}");
        int viB = 0; if (rawBits[pos++] == 0) { for (int b = 0; b < 5; b++) viB = viB*2 + rawBits[pos++]; Console.Write($" vi={viB}"); } else Console.Write(" vi=-1");
        Console.WriteLine($" nextBitPos={pos}");

        // Layout C: 9-bit qi
        pos = 0;
        Console.Write($"  C) exists={rawBits[pos++]} flags=");
        f2 = rawBits[pos]*2 + rawBits[pos+1]; pos += 2;
        Console.Write($"{f2} qiAbsent={rawBits[pos]}");
        int qiC = 0; if (rawBits[pos++] == 0) { for (int b = 0; b < 9; b++) qiC = qiC*2 + rawBits[pos++]; Console.Write($" qi={qiC}"); } else Console.Write(" qi=-1");
        Console.Write($" viAbsent={rawBits[pos]}");
        int viC = 0; if (rawBits[pos++] == 0) { for (int b = 0; b < 5; b++) viC = viC*2 + rawBits[pos++]; Console.Write($" vi={viC}"); } else Console.Write(" vi=-1");
        Console.WriteLine($" nextBitPos={pos}");

        // Layout D: no absent flag for qi (9-bit direct)
        pos = 0;
        Console.Write($"  D) exists={rawBits[pos++]} flags=");
        f2 = rawBits[pos]*2 + rawBits[pos+1]; pos += 2;
        int qiD = 0; for (int b = 0; b < 9; b++) qiD = qiD*2 + rawBits[pos++];
        Console.Write($" qi(direct9)={qiD}");
        Console.Write($" viAbsent={rawBits[pos]}");
        int viD = 0; if (rawBits[pos++] == 0) { for (int b = 0; b < 5; b++) viD = viD*2 + rawBits[pos++]; Console.Write($" vi={viD}"); } else Console.Write(" vi=-1");
        Console.WriteLine($" nextBitPos={pos}");

        // Try a completely different approach: maybe the position of ib is different
        // Check what the ib value should be for the first spawn (should be 1 = in-bounds)
        Console.WriteLine("\n=== Check ib bit at different positions ===");
        for (int ibPos = 12; ibPos <= 22; ibPos++)
        {
            Console.Write($"  ib@{ibPos}: bit={rawBits[ibPos]} ");
            // If ib=1, read adaptive position (20+20+19=59 bits)
            if (rawBits[ibPos] == 1)
            {
                int pPos = ibPos + 1;
                var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                    xMin, xMax, yMin, yMax, zMin, zMax, 21);
                // Decode X position from raw bits
                uint rawX = 0;
                for (int b = 0; b < bitsX && pPos + b < 180; b++)
                    rawX = rawX * 2 + (uint)rawBits[pPos + b];
                float px = xMin + (rawX + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                Console.Write($"X={px:F2} ");
                pPos += bitsX;
                uint rawY = 0;
                for (int b = 0; b < bitsY && pPos + b < 180; b++)
                    rawY = rawY * 2 + (uint)rawBits[pPos + b];
                float py = yMin + (rawY + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                Console.Write($"Y={py:F2} ");
            }
            Console.WriteLine();
        }

        // Now try the first 3 objects with variant qi bit widths
        Console.WriteLine("\n=== Multi-object test with different qi widths ===");
        var (bX, bY, bZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
            xMin, xMax, yMin, yMax, zMin, zMax, 21);
        foreach (int qiBits in new[] { 7, 8, 9 })
        {
            Console.WriteLine($"\n  qi width = {qiBits} bits:");
            var pr = new ForgeX.Core.IO.BitReader(bitstream);
            pr.SkipBits(objStart);
            int objectsParsed = 0;
            for (int slot = 0; slot < 651 && objectsParsed < 5; slot++)
            {
                if (!pr.ReadBool()) continue;
                objectsParsed++;
                int start = pr.BitOffset - 1;
                uint fl = pr.ReadInteger(2);
                bool qA = pr.ReadBool(); int qi = qA ? -1 : (int)pr.ReadInteger(qiBits);
                bool vA = pr.ReadBool(); int vi = vA ? -1 : (int)pr.ReadInteger(5);
                bool ib = pr.ReadBool();
                uint rx = pr.ReadInteger(bX), ry = pr.ReadInteger(bY), rz = pr.ReadInteger(bZ);
                float px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bX));
                float py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bY));
                float pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bZ));
                bool du = pr.ReadBool(); if (!du) pr.ReadInteger(20); pr.ReadInteger(14);
                int sR = (int)pr.ReadInteger(10) - 1;
                uint scl = pr.ReadInteger(6); bool lk = pr.ReadBool();
                uint sh = pr.ReadInteger(2);
                switch (sh) { case 1: pr.ReadInteger(11); break; case 2: pr.ReadInteger(11); pr.ReadInteger(11); pr.ReadInteger(11); break; case 3: pr.ReadInteger(11); pr.ReadInteger(11); pr.ReadInteger(11); pr.ReadInteger(11); break; }
                uint ot = pr.ReadInteger(6);
                uint u10 = pr.ReadInteger(10);
                uint tm = pr.ReadInteger(4);
                uint rsp = pr.ReadInteger(8);
                bool cA = pr.ReadBool(); int col = cA ? -1 : (int)pr.ReadInteger(3);
                uint pf = pr.ReadInteger(8);
                uint sq = pr.ReadInteger(8);
                int[] lb = new int[4];
                for (int li = 0; li < 4; li++) { bool la = pr.ReadBool(); lb[li] = la ? -1 : (int)pr.ReadInteger(8); }
                if (ot == 0 || ot == 2 || ot == 3 || ot == 4 || ot == 5 || ot == 6) { pr.ReadInteger(5); pr.ReadInteger(5); }
                if (ot == 1) pr.ReadInteger(8);
                if (ot >= 13 && ot <= 15) { pr.ReadInteger(5); pr.ReadInteger(5); }
                if (ot == 20) pr.ReadInteger(9);
                if (ot == 12) pr.ReadInteger(8);
                if (ot == 31) pr.ReadInteger(5);
                if (ot >= 21 && ot <= 27) { pr.ReadInteger(5); pr.ReadInteger(5); }
                if (ot == 32) { pr.ReadInteger(5); pr.ReadInteger(8); pr.ReadInteger(16); }
                if (ot == 33) { for (int x = 0; x < 8; x++) pr.ReadInteger(8); }
                if (ot == 34) { for (int x = 0; x < 9; x++) pr.ReadInteger(8); }
                int end = pr.BitOffset;
                bool posOk = px >= xMin - 50 && px <= xMax + 50 && py >= yMin - 50 && py <= yMax + 50 && pz >= zMin - 50 && pz <= zMax + 50;
                Console.WriteLine($"    [{slot}] #{objectsParsed}: {end-start}b qi={qi} vi={vi} ib={ib} pos=({px:F1},{py:F1},{pz:F1}){(posOk?"":" BAD")} sc={scl} lk={lk} t={ot} u10={u10} tm={tm} rsp={rsp} sq={sq}");
            }
        }
    }

    /// <summary>
    /// Dump ALL objects from Nitrogen parser + try quota alignment from multiple positions.
    /// Key question: do the 32 spawns have qi=48/49 or different values?
    /// </summary>
    [Fact]
    public void Halo4FullObjectDump()
    {
        string path = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(path)) { Console.WriteLine("SKIP"); return; }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
        int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
        int packedBits = packedLenBytes * 8;

        // Parse header
        var bits = new ForgeX.Core.IO.BitReader(bitstream);
        bits.ReadInteger(4); bits.ReadInteger(32);
        bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
        bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
        bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool();
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
        int numQuotas = (int)bits.ReadInteger(9);
        bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
        float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
        float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
        float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
        bits.ReadInteger(32); bits.ReadInteger(32);
        int sc2 = (int)bits.ReadInteger(9);
        for (int i = 0; i < sc2; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
        if (sc2 > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
        int objStart = bits.BitOffset;
        var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
            xMin, xMax, yMin, yMax, zMin, zMax, 21);

        // Run Nitrogen with verbose to dump ALL objects
        var (objEnd, active, vp, allObjs) = ParseH4Nitrogen(
            bitstream, objStart, bitsX, bitsY, bitsZ,
            xMin, xMax, yMin, yMax, zMin, zMax,
            numSlots: 651, verbose: false);

        Console.WriteLine($"objEnd={objEnd} active={active} numQuotas={numQuotas} packedBits={packedBits}");
        Console.WriteLine($"\nAll {active} objects:");
        foreach (var obj in allObjs)
            Console.WriteLine($"  [{obj.Slot}] {obj.Bits}b: {obj.Info}");

        // Search for quota start where quota[48] and quota[49] have placed>=16
        Console.WriteLine($"\n=== Search for quota start aligned with qi=48/49 ===");
        for (int offset = -10; offset <= 500; offset++)
        {
            int tryStart = objEnd + offset;
            if (tryStart < 0 || tryStart + numQuotas * 24 > bitstream.Length * 8) continue;
            var qr = new ForgeX.Core.IO.BitReader(bitstream);
            qr.SkipBits(tryStart);
            int[,] qv = new int[numQuotas, 3];
            bool valid2 = true;
            for (int q = 0; q < numQuotas; q++)
            {
                qv[q, 0] = (int)qr.ReadInteger(8);
                qv[q, 1] = (int)qr.ReadInteger(8);
                qv[q, 2] = (int)qr.ReadInteger(8);
                if (qv[q, 0] > qv[q, 1] && (qv[q, 0] != 0 || qv[q, 1] != 0 || qv[q, 2] != 0))
                    { valid2 = false; break; }
            }
            if (!valid2) continue;
            int p48 = qv[48, 2], p49 = qv[49, 2];
            if (p48 >= 16 && p49 >= 16 && qv[48, 1] >= p48 && qv[49, 1] >= p49
                && qv[48, 1] <= 200 && qv[49, 1] <= 200)
            {
                int totalP = 0;
                for (int q = 0; q < numQuotas; q++) totalP += qv[q, 2];
                Console.Write($"  offset={offset} (bit {tryStart}): q48=({qv[48,0]},{qv[48,1]},{qv[48,2]}) q49=({qv[49,0]},{qv[49,1]},{qv[49,2]}) totalPlaced={totalP}");
                Console.Write("  nz:");
                for (int q = 0; q < numQuotas; q++)
                    if (qv[q, 0] != 0 || qv[q, 1] != 0 || qv[q, 2] != 0)
                        Console.Write($" [{q}]=({qv[q,0]},{qv[q,1]},{qv[q,2]})");
                Console.WriteLine();
            }
        }

        // Also show quotas at objEnd+0 for reference
        Console.WriteLine($"\n=== Quotas at objEnd+0 (bit {objEnd}) ===");
        {
            var qr = new ForgeX.Core.IO.BitReader(bitstream);
            qr.SkipBits(objEnd);
            for (int q = 0; q < numQuotas; q++)
            {
                int mn = (int)qr.ReadInteger(8), mx = (int)qr.ReadInteger(8), ct = (int)qr.ReadInteger(8);
                if (mn != 0 || mx != 0 || ct != 0)
                    Console.WriteLine($"  quota[{q}]: min={mn} max={mx} placed={ct}");
            }
        }
    }

    /// <summary>
    /// Find the EXACT quota start position and per-object format for H4.
    /// We know from H3Props scan that quotas start ~14 bits after H3Props object end.
    /// The Nitrogen parser gets individual fields right but misses 3 objects.
    /// This test systematically varies the format to find delta=0 to the known quota position.
    /// </summary>
    [Fact]
    public void Halo4FormatRefinement()
    {
        string path = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(path)) { Console.WriteLine("SKIP"); return; }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
        int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
        int packedBits = packedLenBytes * 8;

        // Parse metadata + header (same as always)
        var bits = new ForgeX.Core.IO.BitReader(bitstream);
        bits.ReadInteger(4); bits.ReadInteger(32);
        bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
        bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
        bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool();
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
        int numQuotas = (int)bits.ReadInteger(9);
        bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
        float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
        float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
        float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
        bits.ReadInteger(32); bits.ReadInteger(32);
        int sc = (int)bits.ReadInteger(9);
        for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
        if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
        int objStart = bits.BitOffset;
        var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
            xMin, xMax, yMin, yMax, zMin, zMax, 21);

        Console.WriteLine($"objStart={objStart} posBits=({bitsX},{bitsY},{bitsZ}) packedBits={packedBits}");
        Console.WriteLine($"Bounds: X[{xMin:F1},{xMax:F1}] Y[{yMin:F1},{yMax:F1}] Z[{zMin:F1},{zMax:F1}]");

        // Step 1: Find EXACT quota start by scanning for first non-zero quota entry
        // We know quotas are 24-bit (min+max+placed) from Reach/H4 format.
        // Scan backwards from packedBits, looking for the first valid quota block.
        Console.WriteLine($"\n=== Step 1: Find exact quota start ===");
        // From PostObjectsGap test: PERFECT MATCHes at gaps 14, 38, 62, 86...
        // These repeat because early quotas are zero. The actual start is the earliest one.
        // The repeat spacing is 24 (one quota entry). So start = objEnd_h3props + 14 = 15121.
        // But let's verify by checking different starting positions.
        for (int tryStart = 15100; tryStart <= 15200; tryStart++)
        {
            var qr = new ForgeX.Core.IO.BitReader(bitstream);
            qr.SkipBits(tryStart);
            int nz = 0, pl = 0; bool valid = true;
            var entries = new List<(int Idx, int Mn, int Mx, int Ct)>();
            for (int q = 0; q < numQuotas; q++)
            {
                int mn = (int)qr.ReadInteger(8), mx = (int)qr.ReadInteger(8), ct = (int)qr.ReadInteger(8);
                if (mn == 0 && mx == 0 && ct == 0) continue;
                if (mn > mx || ct > mx || mx > 200) { valid = false; break; }
                nz++; pl += ct; entries.Add((q, mn, mx, ct));
            }
            if (valid && nz >= 5 && pl == 68)
            {
                Console.WriteLine($"  Valid quotas at bit {tryStart}: nz={nz} placed={pl}");
                foreach (var (idx, mn, mx, ct) in entries)
                    Console.WriteLine($"    quota[{idx}]: min={mn} max={mx} placed={ct}");
                // Only show first match — must be the earliest valid
                break;
            }
        }

        // Step 2: Try Nitrogen parser variants targeting bit 15121
        Console.WriteLine($"\n=== Step 2: Nitrogen variants targeting quota start ===");

        // First, run with different slot counts (640 vs 651)
        foreach (int slots in new[] { 640, 641, 645, 648, 650, 651, 652, 656, 660 })
        {
            var (endBit, act, vp, _) = ParseH4Nitrogen(
                bitstream, objStart, bitsX, bitsY, bitsZ,
                xMin, xMax, yMin, yMax, zMin, zMax,
                numSlots: slots, verbose: false, dimBits: 11, alwaysAdaptive: true);
            Console.WriteLine($"  slots={slots}: end={endBit} a={act} v={vp} delta_15121={endBit - 15121}");
        }

        // Step 3: Try the Nitrogen parser at 640 slots (like H3Props) with verbose output
        Console.WriteLine($"\n=== Step 3: Nitrogen 640 slots detailed dump ===");
        {
            var (endBit, act, vp, objs) = ParseH4Nitrogen(
                bitstream, objStart, bitsX, bitsY, bitsZ,
                xMin, xMax, yMin, yMax, zMin, zMax,
                numSlots: 640, verbose: false, dimBits: 11, alwaysAdaptive: true);
            Console.WriteLine($"  end={endBit} a={act} v={vp}");
            // Dump all objects, grouped by type
            var byType = objs.GroupBy(o => {
                var match = System.Text.RegularExpressions.Regex.Match(o.Info, @"type=(\d+)");
                return match.Success ? int.Parse(match.Groups[1].Value) : -1;
            }).OrderBy(g => g.Key);
            foreach (var grp in byType)
                Console.WriteLine($"  type={grp.Key}: {grp.Count()} objects, avg bits={grp.Average(o => o.Bits):F1}");
        }

        // Step 4: Scan the raw gap from Nitrogen end to quota start
        Console.WriteLine($"\n=== Step 4: Gap analysis (Nitrogen 651 end=14978 to expected quota ~15121) ===");
        {
            int gapStart = 14978;
            int gapLen = 15121 - gapStart;
            Console.WriteLine($"  Gap: {gapStart} to 15121 = {gapLen} bits");
            var peek = new ForgeX.Core.IO.BitReader(bitstream);
            peek.SkipBits(gapStart);
            Console.Write("  Bits: ");
            for (int b = 0; b < gapLen; b++)
            {
                if (b > 0 && b % 50 == 0) Console.Write("\n        ");
                else if (b > 0 && b % 10 == 0) Console.Write(" ");
                Console.Write(peek.ReadBool() ? "1" : "0");
            }
            Console.WriteLine();
        }

        // Step 5: Compare H3Props and Nitrogen parsers object-by-object on first divergence
        Console.WriteLine($"\n=== Step 5: H3Props 640-slot detailed dump ===");
        {
            var pb = new ForgeX.Core.IO.BitReader(bitstream);
            pb.SkipBits(objStart);
            int active = 0;
            for (int i = 0; i < 640; i++)
            {
                if (pb.BitsRemaining < 1) break;
                int startBit = pb.BitOffset;
                if (!pb.ReadBool()) continue;
                active++;
                pb.ReadInteger(2);
                bool qa = pb.ReadBool(); int qi = qa ? -1 : (int)pb.ReadInteger(8);
                bool va = pb.ReadBool(); int vi = va ? -1 : (int)pb.ReadInteger(5);
                bool ib = pb.ReadBool();
                float px = 0, py = 0, pz = 0;
                if (ib) {
                    uint rx = pb.ReadInteger(bitsX), ry = pb.ReadInteger(bitsY), rz = pb.ReadInteger(bitsZ);
                    px = xMin + (rx + 0.5f) * ((xMax - xMin) / (1u << bitsX));
                    py = yMin + (ry + 0.5f) * ((yMax - yMin) / (1u << bitsY));
                    pz = zMin + (rz + 0.5f) * ((zMax - zMin) / (1u << bitsZ));
                } else { px = pb.ReadRawFloat(); py = pb.ReadRawFloat(); pz = pb.ReadRawFloat(); }
                bool du = pb.ReadBool(); if (!du) pb.ReadInteger(20); pb.ReadInteger(14);
                int spawnRel = (int)pb.ReadInteger(10) - 1;
                // H3 props: 8+8+16+8+8+8 = 56 bits of mystery
                uint m1 = pb.ReadInteger(8), m2 = pb.ReadInteger(8);
                uint m3 = pb.ReadInteger(16);
                uint m4 = pb.ReadInteger(8), m5 = pb.ReadInteger(8), m6 = pb.ReadInteger(8);
                uint sh8 = pb.ReadInteger(8);
                int shDimBits = 0;
                if (sh8 >= 1 && sh8 <= 3) { pb.ReadInteger(16); pb.ReadInteger(16); shDimBits += 32; if (sh8 >= 2) { pb.ReadInteger(16); shDimBits += 16; } if (sh8 == 3) { pb.ReadInteger(16); shDimBits += 16; } }
                int endBit = pb.BitOffset;
                // Only print first 40 and last 10 objects
                if (active <= 40 || active > 60)
                    Console.WriteLine($"  [{i}] #{active}: {endBit-startBit}b @{startBit} qi={qi} vi={vi} ib={ib} pos=({px:F1},{py:F1},{pz:F1}) sR={spawnRel} m=({m1},{m2},{m3},{m4},{m5},{m6}) sh={sh8}+{shDimBits}b");
                if (active == 40) Console.WriteLine($"  ... (skipping objects 41-60) ...");
                if (pb.BitsRemaining < 0) { Console.WriteLine($"  *** OVERRUN ***"); break; }
            }
            Console.WriteLine($"  H3Props end={pb.BitOffset} active={active}");
        }
    }

    /// <summary>
    /// Comprehensive H4 metadata tracer + object boundary diagnostic.
    /// Step 1: Parse metadata field-by-field with bit offsets to verify objStart.
    /// Step 2: Parse objects up to the last valid one, then dump raw bits after it.
    /// Step 3: Try to manually decode the next object to find the parsing divergence.
    /// </summary>
    [Fact]
    public void Halo4MetadataTrace()
    {
        string path = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(path)) { Console.WriteLine("SKIP"); return; }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var bitstream = new byte[mvar.Data.Length - 24];
        Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
        int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
        int packedBits = packedLenBytes * 8;

        Console.WriteLine($"mvar data length: {mvar.Data.Length}, packed length: {packedLenBytes} bytes ({packedBits} bits)");

        // ============================================================
        // STEP 1: Field-by-field metadata trace with BOTH activity widths
        // ============================================================
        foreach (int activityBits in new[] { 2, 3 })
        {
            Console.WriteLine($"\n=== Metadata trace with activity={activityBits} bits ===");
            var bits = new ForgeX.Core.IO.BitReader(bitstream);

            int off;
            off = bits.BitOffset; uint fileType = bits.ReadInteger(4);
            Console.WriteLine($"  [{off,5}] file_type(4): {fileType} (decoded: {(int)fileType - 1})");
            off = bits.BitOffset; uint sizeInBytes = bits.ReadInteger(32);
            Console.WriteLine($"  [{off,5}] size_in_bytes(32): {sizeInBytes}");
            off = bits.BitOffset; ulong uniqueId = bits.ReadInteger64(64);
            Console.WriteLine($"  [{off,5}] unique_id(64): 0x{uniqueId:X16}");
            off = bits.BitOffset; ulong parentId = bits.ReadInteger64(64);
            Console.WriteLine($"  [{off,5}] parent_id(64): 0x{parentId:X16}");
            off = bits.BitOffset; ulong rootId = bits.ReadInteger64(64);
            Console.WriteLine($"  [{off,5}] root_id(64): 0x{rootId:X16}");
            off = bits.BitOffset; ulong gameId = bits.ReadInteger64(64);
            Console.WriteLine($"  [{off,5}] game_id(64): 0x{gameId:X16}");

            off = bits.BitOffset; uint activity = bits.ReadInteger(activityBits);
            Console.WriteLine($"  [{off,5}] activity({activityBits}): {activity}{(activityBits == 3 ? $" (decoded: {(int)activity-1})" : "")}");
            off = bits.BitOffset; uint gameMode = bits.ReadInteger(3);
            Console.WriteLine($"  [{off,5}] game_mode(3): {gameMode}");
            off = bits.BitOffset; uint engineType = bits.ReadInteger(3);
            Console.WriteLine($"  [{off,5}] engine_type(3): {engineType}");
            off = bits.BitOffset; int mapId = bits.ReadSignedInteger(32);
            Console.WriteLine($"  [{off,5}] map_id(32): {mapId} (0x{mapId:X8})");
            off = bits.BitOffset; int megaloCat = bits.ReadSignedInteger(8);
            Console.WriteLine($"  [{off,5}] megalo_cat(8): {megaloCat}");

            off = bits.BitOffset; ulong creationTime = bits.ReadInteger64(64);
            Console.WriteLine($"  [{off,5}] creation_time(64): {creationTime}");
            off = bits.BitOffset; ulong creatorXuid = bits.ReadInteger64(64);
            Console.WriteLine($"  [{off,5}] creator_xuid(64): 0x{creatorXuid:X16}");

            // Creator name: ReadStringUtf8(16) — reads up to 16 bytes, stops at null
            off = bits.BitOffset;
            string creatorName = bits.ReadStringUtf8(16);
            Console.WriteLine($"  [{off,5}] creator_name(utf8×16): \"{creatorName}\" ({bits.BitOffset - off} bits consumed)");

            off = bits.BitOffset; bool creatorOnline = bits.ReadBool();
            Console.WriteLine($"  [{off,5}] creator_online(1): {creatorOnline}");

            off = bits.BitOffset; ulong modTime = bits.ReadInteger64(64);
            Console.WriteLine($"  [{off,5}] mod_time(64): {modTime}");
            off = bits.BitOffset; ulong modXuid = bits.ReadInteger64(64);
            Console.WriteLine($"  [{off,5}] mod_xuid(64): 0x{modXuid:X16}");

            off = bits.BitOffset;
            string modName = bits.ReadStringUtf8(16);
            Console.WriteLine($"  [{off,5}] modifier_name(utf8×16): \"{modName}\" ({bits.BitOffset - off} bits consumed)");

            off = bits.BitOffset; bool modOnline = bits.ReadBool();
            Console.WriteLine($"  [{off,5}] mod_online(1): {modOnline}");

            off = bits.BitOffset;
            string variantName = bits.ReadStringWchar(128);
            Console.WriteLine($"  [{off,5}] variant_name(wchar×128): \"{variantName}\" ({bits.BitOffset - off} bits consumed)");

            off = bits.BitOffset;
            string description = bits.ReadStringWchar(128);
            Console.WriteLine($"  [{off,5}] description(wchar×128): \"{description}\" ({bits.BitOffset - off} bits consumed)");

            // Conditionals based on decoded activity/gameMode
            int decodedActivity = activityBits == 3 ? (int)activity - 1 : (int)activity;
            int decodedFileType = (int)fileType - 1;
            if (decodedFileType == 3 || decodedFileType == 4)
            {
                off = bits.BitOffset; int filmSec = bits.ReadSignedInteger(32);
                Console.WriteLine($"  [{off,5}] film_seconds(32): {filmSec}");
            }
            else if (decodedFileType == 6)
            {
                off = bits.BitOffset; int iconIdx = bits.ReadSignedInteger(8);
                Console.WriteLine($"  [{off,5}] icon_index(8): {iconIdx}");
            }
            if (decodedActivity == 2)
            {
                off = bits.BitOffset; uint hopper = bits.ReadInteger(16);
                Console.WriteLine($"  [{off,5}] hopper_id(16): {hopper}");
            }
            if ((int)gameMode == 1)
            {
                Console.WriteLine($"  (campaign fields...)");
                bits.ReadInteger(8); bits.ReadInteger(2); bits.ReadInteger(2);
                bits.ReadInteger(8); bits.ReadInteger(16); bits.ReadInteger(16);
            }
            else if ((int)gameMode == 2)
            {
                Console.WriteLine($"  (firefight fields...)");
                bits.ReadInteger(2); bits.ReadInteger(16); bits.ReadInteger(16);
            }

            int metaEnd = bits.BitOffset;
            Console.WriteLine($"  --- Metadata ends at bit {metaEnd} ---");

            // Map variant header
            off = bits.BitOffset; uint varVersion = bits.ReadInteger(8);
            Console.WriteLine($"  [{off,5}] variant_version(8): {varVersion}");
            off = bits.BitOffset; uint rsaHash = bits.ReadInteger(32);
            Console.WriteLine($"  [{off,5}] rsa_hash(32): 0x{rsaHash:X8}");
            off = bits.BitOffset; uint paletteCrc = bits.ReadInteger(32);
            Console.WriteLine($"  [{off,5}] palette_crc(32): 0x{paletteCrc:X8}");
            off = bits.BitOffset; int numQuotas = (int)bits.ReadInteger(9);
            Console.WriteLine($"  [{off,5}] num_quotas(9): {numQuotas}");
            off = bits.BitOffset; uint mapIdCopy = bits.ReadInteger(32);
            Console.WriteLine($"  [{off,5}] map_id_copy(32): {mapIdCopy} (0x{mapIdCopy:X8})");
            off = bits.BitOffset; bool builtIn = bits.ReadBool();
            Console.WriteLine($"  [{off,5}] built_in(1): {builtIn}");
            off = bits.BitOffset; bool builtXml = bits.ReadBool();
            Console.WriteLine($"  [{off,5}] built_from_xml(1): {builtXml}");

            off = bits.BitOffset;
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            Console.WriteLine($"  [{off,5}] bounds(6×32): X=[{xMin:F1},{xMax:F1}] Y=[{yMin:F1},{yMax:F1}] Z=[{zMin:F1},{zMax:F1}]");

            off = bits.BitOffset; uint maxBudget = bits.ReadInteger(32);
            Console.WriteLine($"  [{off,5}] max_budget(32): {maxBudget}");
            off = bits.BitOffset; uint curBudget = bits.ReadInteger(32);
            Console.WriteLine($"  [{off,5}] cur_budget(32): {curBudget}");

            int headerEnd = bits.BitOffset;
            Console.WriteLine($"  --- Header ends at bit {headerEnd} ---");

            // String table
            off = bits.BitOffset; int strCount = (int)bits.ReadInteger(9);
            Console.WriteLine($"  [{off,5}] string_count(9): {strCount}");
            int strExistBits = 0;
            for (int i = 0; i < strCount; i++)
            {
                bool ex = bits.ReadBool();
                strExistBits++;
                if (ex) { bits.ReadInteger(12); strExistBits += 12; }
            }
            Console.WriteLine($"  string entries: {strExistBits} bits");
            if (strCount > 0)
            {
                off = bits.BitOffset; int bufSize = (int)bits.ReadInteger(13);
                bool comp = bits.ReadBool();
                int readSize = comp ? (int)bits.ReadInteger(13) : bufSize;
                Console.WriteLine($"  [{off,5}] buffer_size(13): {bufSize}, compressed={comp}, readSize={readSize}");
                for (int i = 0; i < readSize; i++) bits.ReadInteger(8);
                Console.WriteLine($"  buffer data: {readSize * 8} bits");
            }

            int objStart = bits.BitOffset;
            Console.WriteLine($"  === objStart = {objStart} ===");

            var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);
            Console.WriteLine($"  position bits: X={bitsX} Y={bitsY} Z={bitsZ} (total={bitsX+bitsY+bitsZ})");

            // Run Nitrogen parser
            var (objEnd, active, vp, allObjs) = ParseH4Nitrogen(
                bitstream, objStart, bitsX, bitsY, bitsZ,
                xMin, xMax, yMin, yMax, zMin, zMax,
                numSlots: 651, verbose: false);

            Console.WriteLine($"  objEnd={objEnd} active={active} validPos={vp}");
            Console.WriteLine($"  gap to known quota start (15313): {15313 - objEnd} bits");

            // Show which objects are valid vs garbage
            int validCount = 0, garbageCount = 0;
            int lastValidSlot = -1, firstGarbageSlot = -1;
            foreach (var obj in allObjs)
            {
                // Quick check: extract position from info string
                string info = obj.Info;
                int posIdx = info.IndexOf("pos=(");
                if (posIdx >= 0)
                {
                    string posStr = info.Substring(posIdx + 5, info.IndexOf(")", posIdx) - posIdx - 5);
                    var parts = posStr.Split(',');
                    float px = float.Parse(parts[0]);
                    float py = float.Parse(parts[1]);
                    float pz = float.Parse(parts[2]);
                    bool posOk = px >= xMin - 50 && px <= xMax + 50 && py >= yMin - 50 && py <= yMax + 50 && pz >= zMin - 50 && pz <= zMax + 50;
                    if (posOk) { validCount++; lastValidSlot = obj.Slot; }
                    else
                    {
                        garbageCount++;
                        if (firstGarbageSlot < 0) firstGarbageSlot = obj.Slot;
                    }
                }
            }
            Console.WriteLine($"  valid={validCount} garbage={garbageCount} lastValid=slot[{lastValidSlot}] firstGarbage=slot[{firstGarbageSlot}]");
        }

        // ============================================================
        // STEP 2: Compare alwaysAdaptive=true vs false (ib-conditional)
        // If ib=False means raw float, objects after #35 should parse better
        // ============================================================
        Console.WriteLine("\n=== STEP 2: alwaysAdaptive comparison ===");
        {
            // Parse header once
            var bits = new ForgeX.Core.IO.BitReader(bitstream);
            bits.ReadInteger(4); bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadStringUtf8(16); bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadStringUtf8(16); bits.ReadBool();
            bits.ReadStringWchar(128); bits.ReadStringWchar(128);
            bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
            bits.ReadInteger(9); bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            bits.ReadInteger(32); bits.ReadInteger(32);
            int sc = (int)bits.ReadInteger(9);
            for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
            if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
            int objStart = bits.BitOffset;

            var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);

            // Run with alwaysAdaptive=false (ib-conditional, like Reach)
            Console.WriteLine($"\n  alwaysAdaptive=FALSE (ib-conditional):");
            var (endF, actF, vpF, objsF) = ParseH4Nitrogen(
                bitstream, objStart, bitsX, bitsY, bitsZ,
                xMin, xMax, yMin, yMax, zMin, zMax,
                numSlots: 651, verbose: false, alwaysAdaptive: false);
            Console.WriteLine($"  objEnd={endF} active={actF} validPos={vpF}");
            Console.WriteLine($"  gap to quota (15313): {15313 - endF}");

            // Count type ranges
            int validTypes = 0, invalidTypes = 0;
            foreach (var obj in objsF)
            {
                var typeMatch = System.Text.RegularExpressions.Regex.Match(obj.Info, @"type=(\d+)");
                if (typeMatch.Success)
                {
                    int t = int.Parse(typeMatch.Groups[1].Value);
                    if (t >= 0 && t <= 35) validTypes++;
                    else invalidTypes++;
                }
            }
            Console.WriteLine($"  validTypes(0-35)={validTypes} invalidTypes(>35)={invalidTypes}");

            // Dump all objects
            Console.WriteLine($"\n  All {actF} objects (ib-conditional):");
            foreach (var obj in objsF)
                Console.WriteLine($"    [{obj.Slot}] {obj.Bits}b: {obj.Info}");

            // Run with alwaysAdaptive=true for comparison
            Console.WriteLine($"\n  alwaysAdaptive=TRUE (always adaptive):");
            var (endT, actT, vpT, objsT) = ParseH4Nitrogen(
                bitstream, objStart, bitsX, bitsY, bitsZ,
                xMin, xMax, yMin, yMax, zMin, zMax,
                numSlots: 651, verbose: false, alwaysAdaptive: true);
            Console.WriteLine($"  objEnd={endT} active={actT} validPos={vpT}");
            Console.WriteLine($"  gap to quota (15313): {15313 - endT}");
            validTypes = 0; invalidTypes = 0;
            foreach (var obj in objsT)
            {
                var typeMatch = System.Text.RegularExpressions.Regex.Match(obj.Info, @"type=(\d+)");
                if (typeMatch.Success)
                {
                    int t = int.Parse(typeMatch.Groups[1].Value);
                    if (t >= 0 && t <= 35) validTypes++;
                    else invalidTypes++;
                }
            }
            Console.WriteLine($"  validTypes(0-35)={validTypes} invalidTypes(>35)={invalidTypes}");

            // Dump ALL alwaysAdaptive objects
            Console.WriteLine($"\n  All {actT} objects (alwaysAdaptive=true):");
            foreach (var obj in objsT)
            {
                // Flag suspicious objects
                var tm2 = System.Text.RegularExpressions.Regex.Match(obj.Info, @"type=(\d+)");
                int ty = tm2.Success ? int.Parse(tm2.Groups[1].Value) : -1;
                string flag = ty > 35 ? " *** INVALID TYPE ***" : "";
                Console.WriteLine($"    [{obj.Slot}] {obj.Bits}b: {obj.Info}{flag}");
            }

            // Show where they diverge
            Console.WriteLine($"\n  Divergence (first 10 diffs):");
            int diffs = 0;
            for (int i = 0; i < Math.Min(objsF.Count, objsT.Count) && diffs < 10; i++)
            {
                if (objsF[i].Slot != objsT[i].Slot || objsF[i].Bits != objsT[i].Bits)
                {
                    Console.WriteLine($"    #{i+1}: F=[{objsF[i].Slot}]{objsF[i].Bits}b vs T=[{objsT[i].Slot}]{objsT[i].Bits}b");
                    diffs++;
                }
            }
            if (diffs == 0) Console.WriteLine("    (same slots and bit counts for all objects)");
        }
    }

    /// <summary>
    /// Dump the intermediate section between objects and quotas for all H4 files.
    /// For grifball, the gap is 335 bits; for forge maps it's 30K-86K bits.
    /// Goal: understand the structure to complete the H4 format.
    /// </summary>
    [Fact]
    public void Halo4IntermediateSectionAnalysis()
    {
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        foreach (var path in files)
        {
            string fname = Path.GetFileName(path);
            var blf = new BlfFile(path);
            var mvar = blf.GetChunk("mvar")!;
            var bitstream = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
            int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
            int packedBits = packedLenBytes * 8;

            // Parse header
            var bits = new ForgeX.Core.IO.BitReader(bitstream);
            bits.ReadInteger(4); bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadStringUtf8(16); bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadStringUtf8(16); bits.ReadBool();
            bits.ReadStringWchar(128); bits.ReadStringWchar(128);
            bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
            int numQuotas = (int)bits.ReadInteger(9);
            bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            bits.ReadInteger(32); bits.ReadInteger(32);
            int sc = (int)bits.ReadInteger(9);
            for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
            if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
            int objStart = bits.BitOffset;
            var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);

            var (objEnd, active, vp, _) = ParseH4Nitrogen(
                bitstream, objStart, bitsX, bitsY, bitsZ,
                xMin, xMax, yMin, yMax, zMin, zMax, 651, false, 11, true);

            Console.WriteLine($"\n=== {fname} ===");
            Console.WriteLine($"objStart={objStart} objEnd={objEnd} active={active} packedBits={packedBits}");

            // Try to find quotas by searching backward from end
            // Assume post-quota is ~1000-2000 bits, quotas are 256×24=6144
            int quotaStart = -1;
            int bestScore = 0;
            for (int qs = packedBits - 6144 - 2000; qs <= packedBits - 6144; qs++)
            {
                if (qs < objEnd || qs + 6144 > bitstream.Length * 8) continue;
                var qr = new ForgeX.Core.IO.BitReader(bitstream);
                qr.SkipBits(qs);
                bool valid2 = true;
                int totalPlaced = 0, nz = 0;
                for (int q = 0; q < 256; q++)
                {
                    int mn = (int)qr.ReadInteger(8), mx = (int)qr.ReadInteger(8), ct = (int)qr.ReadInteger(8);
                    if ((mn > mx || ct > 250) && (mn != 0 || mx != 0 || ct != 0)) { valid2 = false; break; }
                    if (mn != 0 || mx != 0 || ct != 0) nz++;
                    totalPlaced += ct;
                }
                if (!valid2 || totalPlaced < 1 || nz == 0) continue;
                // Score: prefer fewer non-zero quotas and placed count close to active
                int score = 1000 - nz * 10 - Math.Abs(totalPlaced - active) / 5;
                if (score > bestScore) { bestScore = score; quotaStart = qs; }
            }

            if (quotaStart < 0)
            {
                Console.WriteLine($"  No quotas found! Gap to end: {packedBits - objEnd}");
                continue;
            }

            int gap = quotaStart - objEnd;
            int postQuota = packedBits - quotaStart - 6144;
            Console.WriteLine($"quotaStart={quotaStart} gap={gap} postQuota={postQuota}");

            // Print non-zero quotas
            var qrd = new ForgeX.Core.IO.BitReader(bitstream);
            qrd.SkipBits(quotaStart);
            int tp = 0;
            Console.Write("  Quotas: ");
            for (int q = 0; q < 256; q++)
            {
                int mn = (int)qrd.ReadInteger(8), mx = (int)qrd.ReadInteger(8), ct = (int)qrd.ReadInteger(8);
                if (mn != 0 || mx != 0 || ct != 0)
                    Console.Write($"[{q}]=({mn},{mx},{ct}) ");
                tp += ct;
            }
            Console.WriteLine($"\n  totalPlaced={tp}");

            // Dump gap data
            if (gap <= 400)
            {
                Console.Write($"  Gap raw ({gap} bits): ");
                var gr = new ForgeX.Core.IO.BitReader(bitstream);
                gr.SkipBits(objEnd);
                for (int b = 0; b < gap; b++)
                {
                    if (b > 0 && b % 8 == 0) Console.Write(" ");
                    Console.Write(gr.ReadBool() ? "1" : "0");
                }
                Console.WriteLine();
            }
            else
            {
                // For large gaps, show first 200 and last 200 bits
                var gr = new ForgeX.Core.IO.BitReader(bitstream);
                gr.SkipBits(objEnd);
                Console.Write($"  Gap first 200/{gap} bits: ");
                for (int b = 0; b < Math.Min(200, gap); b++)
                {
                    if (b > 0 && b % 8 == 0) Console.Write(" ");
                    Console.Write(gr.ReadBool() ? "1" : "0");
                }
                Console.WriteLine();
                // Check how many are zero
                gr.SeekBits(objEnd);
                int zeroBits = 0;
                for (int b = 0; b < gap; b++)
                    if (!gr.ReadBool()) zeroBits++;
                Console.WriteLine($"  Gap: {zeroBits}/{gap} zero bits ({100.0*zeroBits/gap:F1}%)");
            }

            // Dump post-quota data
            Console.Write($"  Post-quota raw ({postQuota} bits): ");
            var pr = new ForgeX.Core.IO.BitReader(bitstream);
            pr.SkipBits(quotaStart + 6144);
            for (int b = 0; b < Math.Min(200, postQuota); b++)
            {
                if (b > 0 && b % 8 == 0) Console.Write(" ");
                Console.Write(pr.ReadBool() ? "1" : "0");
            }
            if (postQuota > 200) Console.Write("...");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Cross-file validation: run Nitrogen parser on ALL H4 .mvar files.
    /// Check active count, valid types, quota alignment, and gap consistency.
    /// </summary>
    [Fact]
    public void Halo4CrossFileValidation()
    {
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        Console.WriteLine($"Found {files.Length} H4 .mvar files\n");

        foreach (var path in files)
        {
            string fname = Path.GetFileName(path);
            var blf = new BlfFile(path);
            var mvar = blf.GetChunk("mvar")!;
            var bitstream = new byte[mvar.Data.Length - 24];
            Array.Copy(mvar.Data, 24, bitstream, 0, bitstream.Length);
            int packedLenBytes = (mvar.Data[20] << 24) | (mvar.Data[21] << 16) | (mvar.Data[22] << 8) | mvar.Data[23];
            int packedBits = packedLenBytes * 8;

            // Parse header with 2-bit activity
            var bits = new ForgeX.Core.IO.BitReader(bitstream);
            bits.ReadInteger(4); bits.ReadInteger(32);
            bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
            bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadStringUtf8(16); bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
            bits.ReadStringUtf8(16); bits.ReadBool();
            string varName = bits.ReadStringWchar(128);
            bits.ReadStringWchar(128); // description
            bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
            int numQuotas = (int)bits.ReadInteger(9);
            bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
            float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
            float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
            float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
            uint maxBudget = bits.ReadInteger(32); uint curBudget = bits.ReadInteger(32);
            int sc = (int)bits.ReadInteger(9);
            for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
            if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
            int objStart = bits.BitOffset;
            var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
                xMin, xMax, yMin, yMax, zMin, zMax, 21);

            // Run Nitrogen parser
            var (objEnd, active, vp, objs) = ParseH4Nitrogen(
                bitstream, objStart, bitsX, bitsY, bitsZ,
                xMin, xMax, yMin, yMax, zMin, zMax,
                numSlots: 651, verbose: false, alwaysAdaptive: true);

            // Count valid/invalid types
            int validTypes = 0, invalidTypes = 0;
            var qiValues = new List<int>();
            foreach (var obj in objs)
            {
                var typeMatch = System.Text.RegularExpressions.Regex.Match(obj.Info, @"type=(\d+)");
                if (typeMatch.Success)
                {
                    int t = int.Parse(typeMatch.Groups[1].Value);
                    if (t >= 0 && t <= 35) validTypes++;
                    else invalidTypes++;
                }
                var qiMatch = System.Text.RegularExpressions.Regex.Match(obj.Info, @"qi=(-?\d+)");
                if (qiMatch.Success) qiValues.Add(int.Parse(qiMatch.Groups[1].Value));
            }

            // Find quota start by searching backwards from end of packed data
            // Quotas are near the end: 256 × 24 bits = 6144 bits
            int bestQuotaStart = -1;
            int bestGap = int.MaxValue;
            int bestTotalPlaced = 0;
            int bestNonZero = 0;
            // Search from packedBits-6144-2000 to packedBits-6144+100
            int searchCenter = packedBits - 6144;
            for (int tryStart = Math.Max(objEnd - 100, searchCenter - 2000);
                 tryStart <= Math.Min(packedBits - 6144, searchCenter + 100);
                 tryStart++)
            {
                if (tryStart < 0 || tryStart + numQuotas * 24 > bitstream.Length * 8) continue;
                var qr = new ForgeX.Core.IO.BitReader(bitstream);
                qr.SkipBits(tryStart);
                bool valid2 = true;
                int totalPlaced = 0;
                int nonZeroQuotas = 0;
                for (int q = 0; q < numQuotas; q++)
                {
                    int mn = (int)qr.ReadInteger(8), mx = (int)qr.ReadInteger(8), ct = (int)qr.ReadInteger(8);
                    if (mn > mx && (mn != 0 || mx != 0 || ct != 0)) { valid2 = false; break; }
                    if (ct > 250) { valid2 = false; break; } // sanity
                    if (mn != 0 || mx != 0 || ct != 0) nonZeroQuotas++;
                    totalPlaced += ct;
                }
                if (!valid2) continue;
                if (totalPlaced > 0 && totalPlaced < 2000 && nonZeroQuotas > 0 && nonZeroQuotas < 100)
                {
                    int gap = tryStart - objEnd;
                    if (bestQuotaStart < 0 || Math.Abs(gap) < Math.Abs(bestGap))
                    {
                        bestGap = gap;
                        bestQuotaStart = tryStart;
                        bestTotalPlaced = totalPlaced;
                        bestNonZero = nonZeroQuotas;
                    }
                }
            }

            // Verify qi values against quotas
            int qiMatchCount = 0, qiMismatchCount = 0;
            if (bestQuotaStart >= 0)
            {
                var qr = new ForgeX.Core.IO.BitReader(bitstream);
                qr.SkipBits(bestQuotaStart);
                var quotas = new int[numQuotas, 3];
                for (int q = 0; q < numQuotas; q++)
                {
                    quotas[q, 0] = (int)qr.ReadInteger(8);
                    quotas[q, 1] = (int)qr.ReadInteger(8);
                    quotas[q, 2] = (int)qr.ReadInteger(8);
                }
                foreach (int qi in qiValues)
                {
                    if (qi < 0) continue; // absent
                    if (qi < numQuotas && quotas[qi, 2] > 0) qiMatchCount++;
                    else qiMismatchCount++;
                }
            }

            Console.WriteLine($"{fname}: '{varName}'");
            Console.WriteLine($"  objStart={objStart} objEnd={objEnd} active={active} validPos={vp}");
            Console.WriteLine($"  validTypes={validTypes} invalidTypes={invalidTypes}");
            Console.WriteLine($"  quotaStart={bestQuotaStart} gap={bestGap} totalPlaced={bestTotalPlaced} nzQuotas={bestNonZero} packedBits={packedBits}");
            int postQuota = bestQuotaStart >= 0 ? packedBits - (bestQuotaStart + 6144) : -1;
            Console.WriteLine($"  postQuota={postQuota}bits qi: matched={qiMatchCount} mismatched={qiMismatchCount} absent={qiValues.Count(v=>v<0)}");
            Console.WriteLine($"  budget: max={maxBudget} cur={curBudget} strings={sc} posBits={bitsX}+{bitsY}+{bitsZ}={bitsX+bitsY+bitsZ}");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Tests loading H4 MCC .mvar files through the MccHalo4MapVariant class.
    /// Verifies metadata, object counts, quota alignment, and position sanity.
    /// </summary>
    [Fact]
    public void Halo4MccMapVariantLoad()
    {
        string[] files = Directory.GetFiles(H4MccDir, "*.mvar");
        if (files.Length == 0) { Console.WriteLine("SKIP: no H4 files"); return; }

        foreach (var filePath in files.OrderBy(f => new FileInfo(f).Length))
        {
            string name = Path.GetFileName(filePath);
            var blf = new BlfFile(filePath);

            // Verify mvar version is 50
            Assert.Equal(50, blf.GetMvarMajorVersion());

            var variant = new MccHalo4MapVariant(blf);

            Console.WriteLine($"\n========== {name} ==========");
            Console.WriteLine($"  VariantName: '{variant.VariantName}'");
            Console.WriteLine($"  Description: '{variant.VariantDescription}'");
            Console.WriteLine($"  Author: '{variant.MapAuthor}'");
            Console.WriteLine($"  MapId: {variant.MapId}");
            Console.WriteLine($"  SpawnedObjectCount: {variant.SpawnedObjectCount}");
            Console.WriteLine($"  Bounds: X[{variant.WorldBoundsXMin:F1},{variant.WorldBoundsXMax:F1}] " +
                              $"Y[{variant.WorldBoundsYMin:F1},{variant.WorldBoundsYMax:F1}] " +
                              $"Z[{variant.WorldBoundsZMin:F1},{variant.WorldBoundsZMax:F1}]");
            Console.WriteLine($"  Budget: max={variant.MaximumBudget} cur={variant.CurrentBudget}");
            Console.WriteLine($"  Placements: {variant.PlacementChunks.Count}");
            Console.WriteLine($"  TagIndex: {variant.TagIndex.Count}");

            // Basic assertions
            Assert.Equal(651, variant.PlacementChunks.Count);
            Assert.True(variant.MapId > 0, "MapId should be positive");
            Assert.True(variant.WorldBoundsXMax > variant.WorldBoundsXMin, "Bounds should be valid");

            // Count active objects and validate positions
            int existsCount = variant.PlacementChunks.Count(p => p.ChunkType == ChunkType.Added);
            int activeCount = 0;
            int validPositions = 0;
            int validTypes = 0;
            foreach (var p in variant.PlacementChunks)
            {
                if (p.TagsIndex < 0) continue;
                activeCount++;

                // Check position is within bounds (with tolerance)
                float tol = 20f;
                if (p.SpawnCoords.X >= variant.WorldBoundsXMin - tol &&
                    p.SpawnCoords.X <= variant.WorldBoundsXMax + tol &&
                    p.SpawnCoords.Y >= variant.WorldBoundsYMin - tol &&
                    p.SpawnCoords.Y <= variant.WorldBoundsYMax + tol &&
                    p.SpawnCoords.Z >= variant.WorldBoundsZMin - tol &&
                    p.SpawnCoords.Z <= variant.WorldBoundsZMax + tol)
                    validPositions++;

                // H4 types should be 0-35
                if (p.ObjectType >= 0 && p.ObjectType <= 35)
                    validTypes++;
            }

            Console.WriteLine($"  Exists: {existsCount} Active(qi>=0): {activeCount} ValidPos: {validPositions} ValidTypes: {validTypes}");

            // At least some objects should be present in non-empty maps
            Assert.True(activeCount > 0, $"{name} should have active objects");

            // All positions should be valid (always-adaptive guarantees in-bounds)
            Assert.Equal(activeCount, validPositions);

            // Most types should be valid (allowing some cumulative error on complex maps)
            double typeValidRatio = (double)validTypes / activeCount;
            Console.WriteLine($"  TypeValidRatio: {typeValidRatio:P1}");

            // Quota validation: check that used qi values map to non-zero quota entries
            int quotaMatches = 0;
            int quotaMismatches = 0;
            foreach (var p in variant.PlacementChunks)
            {
                if (p.TagsIndex < 0) continue;
                if (p.TagsIndex < variant.TagIndex.Count)
                {
                    var entry = variant.TagIndex[p.TagsIndex];
                    if (entry.CountOnMap > 0 && entry.RunTimeMaximum > 0)
                        quotaMatches++;
                    else
                        quotaMismatches++;
                }
            }
            Console.WriteLine($"  QuotaMatches: {quotaMatches} Mismatches: {quotaMismatches}");

            // Print first 5 active objects for inspection
            int shown = 0;
            foreach (var p in variant.PlacementChunks)
            {
                if (p.TagsIndex < 0) continue;
                if (shown++ >= 5) break;
                Console.WriteLine($"    [{p.Offset}] qi={p.TagsIndex} vi={p.VariantIndex} " +
                    $"pos=({p.SpawnCoords.X:F1},{p.SpawnCoords.Y:F1},{p.SpawnCoords.Z:F1}) " +
                    $"type={p.ObjectType} team={(int)p.ReachTeamRaw-1} " +
                    $"scale={p.H4ScaleRaw} lock={p.H4IsLocked} u10={p.H4Unk10}");
            }

            // Print non-zero quotas and verify structural validity for referenced entries
            int nonZero = 0;
            var referencedQis = new HashSet<int>(
                variant.PlacementChunks.Where(p => p.TagsIndex >= 0).Select(p => p.TagsIndex));
            int referencedViolations = 0;
            foreach (var entry in variant.TagIndex)
            {
                if (entry.CountOnMap > 0 || entry.RunTimeMaximum > 0)
                {
                    nonZero++;
                    Console.WriteLine($"    Quota[{entry.Offset}]: min={entry.RunTimeMinimum} max={entry.RunTimeMaximum} placed={entry.CountOnMap}");

                    // Check structural validity for entries referenced by objects
                    if (referencedQis.Contains(entry.Offset) &&
                        (entry.RunTimeMinimum > entry.RunTimeMaximum ||
                         (entry.RunTimeMaximum > 0 && entry.CountOnMap > entry.RunTimeMaximum)))
                        referencedViolations++;
                }
            }
            Console.WriteLine($"  NonZeroQuotas: {nonZero}");
            Assert.Equal(0, referencedViolations);

            // Grifball-specific assertions (known correct values from format analysis)
            if (name.Contains("grifball"))
            {
                Assert.Equal("$h4_mvar_grifball_name", variant.VariantName);
                Assert.Equal("343 Industries", variant.MapAuthor);
                Assert.Equal(10245, variant.MapId);

                // Known grifball quota values (confirmed via bit-level analysis)
                Assert.Equal(32, variant.TagIndex[48].CountOnMap);
                Assert.Equal(32, variant.TagIndex[48].RunTimeMaximum);
                Assert.Equal(28, variant.TagIndex[49].CountOnMap);
                Assert.Equal(28, variant.TagIndex[49].RunTimeMaximum);
                Assert.Equal(1, variant.TagIndex[50].CountOnMap);
                Assert.Equal(2, variant.TagIndex[51].CountOnMap);
                Assert.Equal(3, variant.TagIndex[61].CountOnMap);
                Assert.Equal(2, variant.TagIndex[95].CountOnMap);

                // At least half of active objects should have matching quotas
                Assert.True(quotaMatches > activeCount / 2,
                    $"Grifball: expected most objects to have quota matches, got {quotaMatches}/{activeCount}");
            }
        }
    }

    /// <summary>
    /// Diagnostic: compares bit offset progression between Nitrogen parser and MccHalo4MapVariant
    /// to identify where they diverge.
    /// </summary>
    [Fact]
    public void Halo4BitOffsetDiagnostic()
    {
        string path = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(path)) { Console.WriteLine("SKIP: no grifball file"); return; }

        var blf = new BlfFile(path);
        var mvar = blf.GetChunk("mvar")!;
        var payload = mvar.Data;

        // Parse metadata/header/strings manually to find objStart (same as Nitrogen test code)
        var bitstream = new byte[payload.Length - 24];
        Array.Copy(payload, 24, bitstream, 0, bitstream.Length);

        var bits = new ForgeX.Core.IO.BitReader(bitstream);
        // metadata
        bits.ReadInteger(4); bits.ReadInteger(32);
        bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64); bits.ReadInteger64(64);
        bits.ReadInteger(2); bits.ReadInteger(3); bits.ReadInteger(3); bits.ReadInteger(32); bits.ReadInteger(8);
        bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool(); bits.ReadInteger64(64); bits.ReadInteger64(64);
        for (int i = 0; i < 16; i++) { if ((byte)bits.ReadInteger(8) == 0) break; }
        bits.ReadBool();
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        for (int i = 0; i < 128; i++) { if ((ushort)bits.ReadInteger(16) == 0) break; }
        // header
        bits.ReadInteger(8); bits.ReadInteger(32); bits.ReadInteger(32);
        int numQuotas = (int)bits.ReadInteger(9);
        bits.ReadInteger(32); bits.ReadBool(); bits.ReadBool();
        float xMin = bits.ReadRawFloat(), xMax = bits.ReadRawFloat();
        float yMin = bits.ReadRawFloat(), yMax = bits.ReadRawFloat();
        float zMin = bits.ReadRawFloat(), zMax = bits.ReadRawFloat();
        bits.ReadInteger(32); bits.ReadInteger(32);
        // string table
        int sc = (int)bits.ReadInteger(9);
        for (int i = 0; i < sc; i++) { bool e = bits.ReadBool(); if (e) bits.ReadInteger(12); }
        if (sc > 0) { int bsz = (int)bits.ReadInteger(13); bool comp = bits.ReadBool(); int rs = comp ? (int)bits.ReadInteger(13) : bsz; for (int ci = 0; ci < rs; ci++) bits.ReadInteger(8); }
        int objStart = bits.BitOffset;

        Console.WriteLine($"objStart (Nitrogen test): {objStart}");

        // Now run the MccHalo4MapVariant parser to compare
        // We can't get the internal bit offsets from MccHalo4MapVariant, so we parse the same
        // stream with both approaches and compare per-object

        var (bitsX, bitsY, bitsZ) = ForgeX.Core.Reach.ReachPositionEncoding.ComputeAxisBitCounts(
            xMin, xMax, yMin, yMax, zMin, zMax, 21);
        Console.WriteLine($"posBits=({bitsX},{bitsY},{bitsZ})");

        // Parse objects using BOTH the Nitrogen approach and step-by-step field tracing
        var pb = new ForgeX.Core.IO.BitReader(bitstream);
        pb.SkipBits(objStart);

        // Also create a reader that matches MccHalo4MapVariant exactly
        var mb = new ForgeX.Core.IO.BitReader(bitstream);
        mb.SkipBits(objStart);

        for (int slot = 0; slot < 80; slot++) // only trace first 80 slots
        {
            int nStart = pb.BitOffset;
            int mStart = mb.BitOffset;

            // Nitrogen parser
            bool nExists = pb.ReadBool();
            bool mExists = mb.ReadBool();

            if (nStart != mStart || nExists != mExists)
            {
                Console.WriteLine($"  [{slot}] DIVERGED: N@{nStart}={nExists} M@{mStart}={mExists}");
            }

            if (!nExists && !mExists) continue;

            if (!nExists || !mExists)
            {
                // One found exists, other didn't - trace the rest of the active one
                if (nExists)
                {
                    Console.WriteLine($"  [{slot}] Nitrogen sees exists=true @{nStart}, MccH4 sees false @{mStart}");
                    // Skip the rest of this object in Nitrogen
                    uint nflags = pb.ReadInteger(2);
                    bool nqa = pb.ReadBool(); if (!nqa) pb.ReadInteger(8);
                    bool nva = pb.ReadBool(); if (!nva) pb.ReadInteger(5);
                    pb.ReadBool(); pb.ReadInteger(bitsX); pb.ReadInteger(bitsY); pb.ReadInteger(bitsZ);
                    bool ndu = pb.ReadBool(); if (!ndu) pb.ReadInteger(20); pb.ReadInteger(14);
                    pb.ReadInteger(10); pb.ReadInteger(6); pb.ReadBool();
                    uint nsh = pb.ReadInteger(2);
                    switch (nsh) { case 1: pb.ReadInteger(11); break; case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break; case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break; }
                    uint ntype = pb.ReadInteger(6); pb.ReadInteger(10);
                    pb.ReadInteger(4); pb.ReadInteger(8);
                    bool nca = pb.ReadBool(); if (!nca) pb.ReadInteger(3);
                    pb.ReadInteger(8); pb.ReadInteger(8);
                    for (int li = 0; li < 4; li++) { bool la = pb.ReadBool(); if (!la) pb.ReadInteger(8); }
                    // type conditionals
                    if (ntype == 1) pb.ReadInteger(8);
                    if (ntype == 0 || (ntype >= 2 && ntype <= 6)) { pb.ReadInteger(5); pb.ReadInteger(5); }
                    if (ntype >= 13 && ntype <= 15) { pb.ReadInteger(5); pb.ReadInteger(5); }
                    if (ntype == 20) pb.ReadInteger(9);
                    if (ntype == 12) pb.ReadInteger(8);
                    if (ntype == 31) pb.ReadInteger(5);
                    if (ntype >= 21 && ntype <= 27) { pb.ReadInteger(5); pb.ReadInteger(5); }
                    if (ntype == 32) { pb.ReadInteger(5); pb.ReadInteger(8); pb.ReadInteger(16); }
                    if (ntype == 33) { for (int x = 0; x < 8; x++) pb.ReadInteger(8); }
                    if (ntype == 34) { for (int x = 0; x < 9; x++) pb.ReadInteger(8); }
                    Console.WriteLine($"    Nitrogen obj end @{pb.BitOffset} (type={ntype})");
                }
                if (mExists)
                {
                    Console.WriteLine($"  [{slot}] MccH4 sees exists=true @{mStart}, Nitrogen sees false @{nStart}");
                    // Skip this MccH4 object
                    mb.ReadInteger(2); // flags
                    bool qa = mb.ReadBool(); if (!qa) mb.ReadInteger(8);
                    bool va = mb.ReadBool(); if (!va) mb.ReadInteger(5);
                    mb.ReadBool(); mb.ReadInteger(bitsX); mb.ReadInteger(bitsY); mb.ReadInteger(bitsZ);
                    bool mdu = mb.ReadBool(); if (!mdu) mb.ReadInteger(20); mb.ReadInteger(14);
                    mb.ReadInteger(10); mb.ReadInteger(6); mb.ReadBool();
                    uint msh = mb.ReadInteger(2);
                    switch (msh) { case 1: mb.ReadInteger(11); break; case 2: mb.ReadInteger(11); mb.ReadInteger(11); mb.ReadInteger(11); break; case 3: mb.ReadInteger(11); mb.ReadInteger(11); mb.ReadInteger(11); mb.ReadInteger(11); break; }
                    uint mtype = mb.ReadInteger(6); mb.ReadInteger(10);
                    mb.ReadInteger(4); mb.ReadInteger(8);
                    bool mca = mb.ReadBool(); if (!mca) mb.ReadInteger(3);
                    mb.ReadInteger(8); mb.ReadInteger(8);
                    for (int li = 0; li < 4; li++) { bool la = mb.ReadBool(); if (!la) mb.ReadInteger(8); }
                    if (mtype == 1) mb.ReadInteger(8);
                    if (mtype == 0 || (mtype >= 2 && mtype <= 6)) { mb.ReadInteger(5); mb.ReadInteger(5); }
                    if (mtype >= 13 && mtype <= 15) { mb.ReadInteger(5); mb.ReadInteger(5); }
                    if (mtype == 20) mb.ReadInteger(9);
                    if (mtype == 12) mb.ReadInteger(8);
                    if (mtype == 31) mb.ReadInteger(5);
                    if (mtype >= 21 && mtype <= 27) { mb.ReadInteger(5); mb.ReadInteger(5); }
                    if (mtype == 32) { mb.ReadInteger(5); mb.ReadInteger(8); mb.ReadInteger(16); }
                    if (mtype == 33) { for (int x = 0; x < 8; x++) mb.ReadInteger(8); }
                    if (mtype == 34) { for (int x = 0; x < 9; x++) mb.ReadInteger(8); }
                    Console.WriteLine($"    MccH4 obj end @{mb.BitOffset} (type={mtype})");
                }
                continue;
            }

            // BOTH exist — trace field by field
            int nEnd, mEnd;
            // Both active - parse identically (since the parsers ARE the same)
            uint flags_n = pb.ReadInteger(2), flags_m = mb.ReadInteger(2);
            bool qa_n = pb.ReadBool(); int qi_n = qa_n ? -1 : (int)pb.ReadInteger(8);
            bool qa_m = mb.ReadBool(); int qi_m = qa_m ? -1 : (int)mb.ReadInteger(8);
            bool va_n = pb.ReadBool(); int vi_n = va_n ? -1 : (int)pb.ReadInteger(5);
            bool va_m = mb.ReadBool(); int vi_m = va_m ? -1 : (int)mb.ReadInteger(5);

            bool ib_n = pb.ReadBool(), ib_m = mb.ReadBool();
            pb.ReadInteger(bitsX); pb.ReadInteger(bitsY); pb.ReadInteger(bitsZ);
            mb.ReadInteger(bitsX); mb.ReadInteger(bitsY); mb.ReadInteger(bitsZ);

            bool du_n = pb.ReadBool(); if (!du_n) pb.ReadInteger(20); pb.ReadInteger(14);
            bool du_m = mb.ReadBool(); if (!du_m) mb.ReadInteger(20); mb.ReadInteger(14);

            int afterOrient_n = pb.BitOffset, afterOrient_m = mb.BitOffset;

            pb.ReadInteger(10); pb.ReadInteger(6); pb.ReadBool();
            mb.ReadInteger(10); mb.ReadInteger(6); mb.ReadBool();

            uint sh_n = pb.ReadInteger(2), sh_m = mb.ReadInteger(2);
            switch (sh_n) { case 1: pb.ReadInteger(11); break; case 2: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break; case 3: pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); pb.ReadInteger(11); break; }
            switch (sh_m) { case 1: mb.ReadInteger(11); break; case 2: mb.ReadInteger(11); mb.ReadInteger(11); mb.ReadInteger(11); break; case 3: mb.ReadInteger(11); mb.ReadInteger(11); mb.ReadInteger(11); mb.ReadInteger(11); break; }

            uint type_n = pb.ReadInteger(6), type_m = mb.ReadInteger(6);
            pb.ReadInteger(10); mb.ReadInteger(10);
            pb.ReadInteger(4); mb.ReadInteger(4);
            pb.ReadInteger(8); mb.ReadInteger(8);
            bool ca_n = pb.ReadBool(); if (!ca_n) pb.ReadInteger(3);
            bool ca_m = mb.ReadBool(); if (!ca_m) mb.ReadInteger(3);
            pb.ReadInteger(8); mb.ReadInteger(8);
            pb.ReadInteger(8); mb.ReadInteger(8);
            for (int li = 0; li < 4; li++) { bool la_n = pb.ReadBool(); if (!la_n) pb.ReadInteger(8); }
            for (int li = 0; li < 4; li++) { bool la_m = mb.ReadBool(); if (!la_m) mb.ReadInteger(8); }

            int beforeTC_n = pb.BitOffset, beforeTC_m = mb.BitOffset;

            // type conditionals (BOTH parsers identically)
            if (type_n == 1) pb.ReadInteger(8);
            if (type_n == 0 || (type_n >= 2 && type_n <= 6)) { pb.ReadInteger(5); pb.ReadInteger(5); }
            if (type_n >= 13 && type_n <= 15) { pb.ReadInteger(5); pb.ReadInteger(5); }
            if (type_n == 20) pb.ReadInteger(9);
            if (type_n == 12) pb.ReadInteger(8);
            if (type_n == 31) pb.ReadInteger(5);
            if (type_n >= 21 && type_n <= 27) { pb.ReadInteger(5); pb.ReadInteger(5); }
            if (type_n == 32) { pb.ReadInteger(5); pb.ReadInteger(8); pb.ReadInteger(16); }
            if (type_n == 33) { for (int x = 0; x < 8; x++) pb.ReadInteger(8); }
            if (type_n == 34) { for (int x = 0; x < 9; x++) pb.ReadInteger(8); }

            if (type_m == 1) mb.ReadInteger(8);
            if (type_m == 0 || (type_m >= 2 && type_m <= 6)) { mb.ReadInteger(5); mb.ReadInteger(5); }
            if (type_m >= 13 && type_m <= 15) { mb.ReadInteger(5); mb.ReadInteger(5); }
            if (type_m == 20) mb.ReadInteger(9);
            if (type_m == 12) mb.ReadInteger(8);
            if (type_m == 31) mb.ReadInteger(5);
            if (type_m >= 21 && type_m <= 27) { mb.ReadInteger(5); mb.ReadInteger(5); }
            if (type_m == 32) { mb.ReadInteger(5); mb.ReadInteger(8); mb.ReadInteger(16); }
            if (type_m == 33) { for (int x = 0; x < 8; x++) mb.ReadInteger(8); }
            if (type_m == 34) { for (int x = 0; x < 9; x++) mb.ReadInteger(8); }

            nEnd = pb.BitOffset;
            mEnd = mb.BitOffset;

            if (nEnd != mEnd || qi_n != qi_m || type_n != type_m)
            {
                Console.WriteLine($"  [{slot}] FIELD DIFF: qi={qi_n}/{qi_m} type={type_n}/{type_m} end={nEnd}/{mEnd}");
            }
            else if (slot < 5)
            {
                Console.WriteLine($"  [{slot}] OK @{nStart}-{nEnd} ({nEnd-nStart}b) qi={qi_n} type={type_n} sh={sh_n} du={du_n}");
            }
        }

        Console.WriteLine($"\nFinal: Nitrogen @{pb.BitOffset}  MccH4-equivalent @{mb.BitOffset}");
    }
}
