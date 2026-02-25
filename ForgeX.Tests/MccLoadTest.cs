using ForgeX.Core.Blf;
using ForgeX.Core.Halo3;
using ForgeX.Core.Halo4;
using ForgeX.Core.IO;
using ForgeX.Core.Reach;

namespace ForgeX.Tests;

public class MccLoadTest
{
    private const string MvarDir = "/run/media/james/m2-ssd/Program Files (x86)/Steam/steamapps/common/Halo The Master Chief Collection/halo3/map_variants/";

    [Fact]
    public void CanLoadUnpackedMapvFile()
    {
        string path = Path.Combine(MvarDir, "trapped3.mvar");
        if (!File.Exists(path))
        {
            Console.WriteLine("SKIP: MCC .mvar test file not found");
            return;
        }

        // Copy to temp to avoid modifying the original
        var tempFile = Path.GetTempFileName() + ".mvar";
        File.Copy(path, tempFile, true);

        try
        {
            var variant = new MccMapVariant(tempFile);

            Console.WriteLine($"Variant Name: '{variant.VariantName}'");
            Console.WriteLine($"Description: '{variant.VariantDescription}'");
            Console.WriteLine($"Author: '{variant.MapAuthor}'");
            Console.WriteLine($"Map ID: {variant.MapId}");
            Console.WriteLine($"Map Name: {variant.Tags?.MapName ?? "null"}");
            Console.WriteLine($"Spawned Object Count: {variant.SpawnedObjectCount}");
            Console.WriteLine($"Max Budget: {variant.MaximumBudget}");
            Console.WriteLine($"Current Budget: {variant.CurrentBudget}");
            Console.WriteLine($"World Bounds: X[{variant.WorldBoundsXMin},{variant.WorldBoundsXMax}] Y[{variant.WorldBoundsYMin},{variant.WorldBoundsYMax}] Z[{variant.WorldBoundsZMin},{variant.WorldBoundsZMax}]");

            // Basic assertions
            Assert.Equal("Trapped v3.0", variant.VariantName);
            Assert.Equal(30, variant.MapId); // Zanzibar
            Assert.NotNull(variant.Tags);
            Assert.True(variant.Tags!.TagCount > 0);

            // Check placements
            int activePlacements = variant.PlacementChunks.Count(c => c.TagsIndex >= 0);
            Console.WriteLine($"Active Placements: {activePlacements}");
            Assert.True(activePlacements > 0, "Should have active placements");

            // Check tag index
            int activeTagEntries = variant.TagIndex.Count(e => e.Tag != null);
            Console.WriteLine($"Active Tag Index Entries: {activeTagEntries}");
            Assert.True(activeTagEntries > 0, "Should have active tag index entries");

            // Print first few placements
            Console.WriteLine("\nFirst 5 active placements:");
            foreach (var chunk in variant.PlacementChunks.Where(c => c.TagsIndex >= 0).Take(5))
            {
                Console.WriteLine($"  Type={chunk.ChunkType} TagsIdx={chunk.TagsIndex} Pos=({chunk.SpawnCoords.X:F2},{chunk.SpawnCoords.Y:F2},{chunk.SpawnCoords.Z:F2}) YPR=({chunk.SpawnCoords.Yaw:F3},{chunk.SpawnCoords.Pitch:F3},{chunk.SpawnCoords.Roll:F3})");
            }

            // Print first few tag entries
            Console.WriteLine("\nFirst 5 active tag entries:");
            foreach (var entry in variant.TagIndex.Where(e => e.Tag != null).Take(5))
            {
                Console.WriteLine($"  {entry.Tag!.Class}/{entry.Tag.Path} Ident={entry.Ident} Count={entry.CountOnMap} Cost={entry.Cost}");
            }

            variant.CloseIO();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void CanLoadPackedMvarFile()
    {
        // Canvas.mvar — Foundry blank variant by Bungie
        string path = Path.Combine(MvarDir, "130cadd1-d80b-4992-b760-d9602410b377.mvar");
        if (!File.Exists(path))
        {
            Console.WriteLine("SKIP: Packed .mvar test file not found");
            return;
        }

        var tempFile = Path.GetTempFileName() + ".mvar";
        File.Copy(path, tempFile, true);

        try
        {
            var variant = new MccMapVariant(tempFile);

            Console.WriteLine($"Variant Name: '{variant.VariantName}'");
            Console.WriteLine($"Description: '{variant.VariantDescription}'");
            Console.WriteLine($"Author: '{variant.MapAuthor}'");
            Console.WriteLine($"Map ID: {variant.MapId}");
            Console.WriteLine($"Map Name: {variant.Tags?.MapName ?? "null"}");
            Console.WriteLine($"Spawned Object Count: {variant.SpawnedObjectCount}");
            Console.WriteLine($"Max Budget: {variant.MaximumBudget}");
            Console.WriteLine($"Current Budget: {variant.CurrentBudget}");

            // Verified values from Python reference parser
            Assert.Equal("Canvas", variant.VariantName);
            Assert.Equal("Blank", variant.VariantDescription);
            Assert.Equal("Bungie", variant.MapAuthor);
            Assert.Equal(480, variant.MapId); // Foundry
            Assert.NotNull(variant.Tags);
            Assert.Equal("Foundry", variant.Tags!.MapName);
            Assert.Equal(700f, variant.MaximumBudget);
            Assert.Equal(0f, variant.CurrentBudget);

            int activePlacements = variant.PlacementChunks.Count(c => c.TagsIndex >= 0);
            Console.WriteLine($"Active Placements: {activePlacements}");
            Assert.Equal(272, activePlacements);

            int activeTagEntries = variant.TagIndex.Count(e => e.Tag != null);
            Console.WriteLine($"Active Tag Index Entries: {activeTagEntries}");
            Assert.Equal(120, activeTagEntries);

            variant.CloseIO();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void CanLoadPackedMvarFileWithObjects()
    {
        // A larger packed mvar file with many objects
        string path = Path.Combine(MvarDir, "09c677a8-e615-41de-86d1-ee2304447cdf.mvar");
        if (!File.Exists(path))
        {
            Console.WriteLine("SKIP: Packed .mvar test file not found");
            return;
        }

        var tempFile = Path.GetTempFileName() + ".mvar";
        File.Copy(path, tempFile, true);

        try
        {
            var variant = new MccMapVariant(tempFile);

            Console.WriteLine($"Variant Name: '{variant.VariantName}'");
            Console.WriteLine($"Description: '{variant.VariantDescription}'");
            Console.WriteLine($"Author: '{variant.MapAuthor}'");
            Console.WriteLine($"Map ID: {variant.MapId}");
            Console.WriteLine($"Map Name: {variant.Tags?.MapName ?? "null"}");
            Console.WriteLine($"Spawned Object Count: {variant.SpawnedObjectCount}");
            Console.WriteLine($"Max Budget: {variant.MaximumBudget}");
            Console.WriteLine($"Current Budget: {variant.CurrentBudget}");

            int activePlacements = variant.PlacementChunks.Count(c => c.TagsIndex >= 0);
            Console.WriteLine($"Active Placements: {activePlacements}");

            int activeTagEntries = variant.TagIndex.Count(e => e.Tag != null);
            Console.WriteLine($"Active Tag Index Entries: {activeTagEntries}");

            // Print first 5 active placements for diagnostics
            Console.WriteLine("\nFirst 5 active placements:");
            foreach (var chunk in variant.PlacementChunks.Where(c => c.TagsIndex >= 0).Take(5))
                Console.WriteLine($"  Type={chunk.ChunkType} TagsIdx={chunk.TagsIndex} Pos=({chunk.SpawnCoords.X:F2},{chunk.SpawnCoords.Y:F2},{chunk.SpawnCoords.Z:F2}) YPR=({chunk.SpawnCoords.Yaw:F3},{chunk.SpawnCoords.Pitch:F3},{chunk.SpawnCoords.Roll:F3})");

            // Assertions — this file should have meaningful data
            Assert.False(string.IsNullOrEmpty(variant.VariantName));
            Assert.NotEqual(0, variant.MapId);
            Assert.True(activePlacements > 0, "Should have active placements");
            Assert.True(variant.MaximumBudget > 0, "Budget should be positive");

            variant.CloseIO();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void BlfFileDetectsFormat()
    {
        // Test unpacked
        string unpackedPath = Path.Combine(MvarDir, "trapped3.mvar");
        if (File.Exists(unpackedPath))
        {
            var blf = new BlfFile(unpackedPath);
            Assert.Equal(BlfVariantFormat.UnpackedMapv, blf.VariantFormat);
            Assert.True(blf.HasChunk("mapv"));
            Assert.True(blf.HasChunk("_blf"));
            Console.WriteLine($"Unpacked: {blf.Chunks.Count} chunks");
        }

        // Test packed
        string packedPath = Path.Combine(MvarDir, "130cadd1-d80b-4992-b760-d9602410b377.mvar");
        if (File.Exists(packedPath))
        {
            var blf = new BlfFile(packedPath);
            Assert.Equal(BlfVariantFormat.PackedMvar, blf.VariantFormat);
            Assert.True(blf.HasChunk("mvar"));
            Console.WriteLine($"Packed: {blf.Chunks.Count} chunks");
        }
    }

    [Fact]
    public void BitReaderWriterRoundTrip()
    {
        var writer = new BitWriter();

        writer.WriteBool(true);
        writer.WriteBool(false);
        writer.WriteInteger(42, 8);
        writer.WriteInteger(1000, 16);
        writer.WriteSignedInteger(-5, 8);
        writer.WriteRawFloat(3.14159f);
        writer.WriteStringUtf8("Hello", 16);

        byte[] data = writer.ToArray();
        var reader = new BitReader(data);

        Assert.True(reader.ReadBool());
        Assert.False(reader.ReadBool());
        Assert.Equal(42u, reader.ReadInteger(8));
        Assert.Equal(1000u, reader.ReadInteger(16));
        Assert.Equal(-5, reader.ReadSignedInteger(8));
        Assert.Equal(3.14159f, reader.ReadRawFloat());
        Assert.Equal("Hello", reader.ReadStringUtf8(16));
    }

    [Fact]
    public void CanLoadAllMvarFiles()
    {
        if (!Directory.Exists(MvarDir))
        {
            Console.WriteLine("SKIP: MCC map_variants directory not found");
            return;
        }

        var files = Directory.GetFiles(MvarDir, "*.mvar");
        int loaded = 0;

        foreach (var file in files)
        {
            var tempFile = Path.GetTempFileName() + ".mvar";
            File.Copy(file, tempFile, true);

            try
            {
                var variant = new MccMapVariant(tempFile);
                int activePlacements = variant.PlacementChunks.Count(c => c.TagsIndex >= 0);
                string decompNote = variant.DecompressedPath != null ? " (decompressed)" : "";
                Console.WriteLine($"  OK{decompNote}: {Path.GetFileName(file)} -> '{variant.VariantName}' MapId={variant.MapId} Placements={activePlacements}");
                variant.CloseIO();
                loaded++;

                // Clean up decompressed copy if one was created
                if (variant.DecompressedPath != null && File.Exists(variant.DecompressedPath))
                    File.Delete(variant.DecompressedPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL: {Path.GetFileName(file)} -> {ex.GetType().Name}: {ex.Message}");
                Assert.Fail($"Failed to load {Path.GetFileName(file)}: {ex.Message}");
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        Console.WriteLine($"\nLoaded: {loaded}, Total: {files.Length}");
        Assert.True(loaded > 0, "Should load at least one file");
    }

    [Fact]
    public void PaletteResolvesConstructTags()
    {
        string path = Path.Combine(MvarDir, "h3_hardcoreConstruct_ts.mvar");
        if (!File.Exists(path))
        {
            Console.WriteLine("SKIP: Construct test file not found");
            return;
        }

        var tempFile = Path.GetTempFileName() + ".mvar";
        File.Copy(path, tempFile, true);

        try
        {
            var variant = new MccMapVariant(tempFile);
            Assert.Equal(300, variant.MapId); // Construct

            Console.WriteLine($"Map: {variant.Tags?.MapName} (ID={variant.MapId})");
            Console.WriteLine($"Has palettes: {variant.Tags?.HasPalettes}");
            Assert.True(variant.Tags?.HasPalettes, "Construct XML should have palette data");

            int unknown = 0;
            int resolved = 0;
            Console.WriteLine("\nActive tag entries:");
            foreach (var entry in variant.TagIndex.Where(e => e.Tag != null))
            {
                if (entry.Tag!.Class == "unknown")
                    unknown++;
                else
                    resolved++;
                Console.WriteLine($"  [{entry.Tag.TagsIndex}] {entry.Tag.Class}/{entry.Tag.Path} Cost={entry.Cost}");
            }

            Console.WriteLine($"\nResolved: {resolved}, Unknown: {unknown}");
            Assert.True(resolved > 0, "Should have resolved tags via palette");
            Assert.Equal(0, unknown);

            variant.CloseIO();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void PackedMvarRoundTripWrite()
    {
        // Load a packed mvar, write it back, re-read, verify fields match
        string path = Path.Combine(MvarDir, "130cadd1-d80b-4992-b760-d9602410b377.mvar");
        if (!File.Exists(path))
        {
            Console.WriteLine("SKIP: Packed .mvar test file not found");
            return;
        }

        var tempFile = Path.GetTempFileName() + ".mvar";
        File.Copy(path, tempFile, true);

        try
        {
            // Load original
            var original = new MccMapVariant(tempFile);
            var origName = original.VariantName;
            var origDesc = original.VariantDescription;
            var origAuthor = original.MapAuthor;
            var origMapId = original.MapId;
            var origMaxBudget = original.MaximumBudget;
            var origCurrentBudget = original.CurrentBudget;
            int origActivePlacements = original.PlacementChunks.Count(c => c.TagsIndex >= 0);
            int origActiveTagEntries = original.TagIndex.Count(e => e.Tag != null);

            // Save first active placement's data for comparison
            var firstPlacement = original.PlacementChunks.First(c => c.TagsIndex >= 0);
            var origPX = firstPlacement.SpawnCoords.X;
            var origPY = firstPlacement.SpawnCoords.Y;
            var origPZ = firstPlacement.SpawnCoords.Z;
            var origTagsIndex = firstPlacement.TagsIndex;

            // Write it back (re-encodes entire bitstream)
            original.WriteHeader();
            original.CloseIO();

            // Re-read
            var reloaded = new MccMapVariant(tempFile);

            Console.WriteLine($"Original:  '{origName}' MapId={origMapId} Budget={origMaxBudget}/{origCurrentBudget}");
            Console.WriteLine($"Reloaded:  '{reloaded.VariantName}' MapId={reloaded.MapId} Budget={reloaded.MaximumBudget}/{reloaded.CurrentBudget}");

            Assert.Equal(origName, reloaded.VariantName);
            Assert.Equal(origDesc, reloaded.VariantDescription);
            Assert.Equal(origAuthor, reloaded.MapAuthor);
            Assert.Equal(origMapId, reloaded.MapId);
            Assert.Equal(origMaxBudget, reloaded.MaximumBudget);
            Assert.Equal(origCurrentBudget, reloaded.CurrentBudget);

            int reloadedActivePlacements = reloaded.PlacementChunks.Count(c => c.TagsIndex >= 0);
            int reloadedActiveTagEntries = reloaded.TagIndex.Count(e => e.Tag != null);
            Console.WriteLine($"Placements: orig={origActivePlacements} reloaded={reloadedActivePlacements}");
            Console.WriteLine($"Tag Entries: orig={origActiveTagEntries} reloaded={reloadedActiveTagEntries}");
            Assert.Equal(origActivePlacements, reloadedActivePlacements);
            Assert.Equal(origActiveTagEntries, reloadedActiveTagEntries);

            // Verify first placement coordinates survived round-trip (within quantization tolerance)
            var reloadedFirst = reloaded.PlacementChunks.First(c => c.TagsIndex >= 0);
            Console.WriteLine($"First placement: orig=({origPX:F4},{origPY:F4},{origPZ:F4}) idx={origTagsIndex}");
            Console.WriteLine($"                 new=({reloadedFirst.SpawnCoords.X:F4},{reloadedFirst.SpawnCoords.Y:F4},{reloadedFirst.SpawnCoords.Z:F4}) idx={reloadedFirst.TagsIndex}");
            Assert.Equal(origTagsIndex, reloadedFirst.TagsIndex);
            // 16-bit quantization over world bounds gives ~0.01 precision
            Assert.InRange(reloadedFirst.SpawnCoords.X, origPX - 0.05f, origPX + 0.05f);
            Assert.InRange(reloadedFirst.SpawnCoords.Y, origPY - 0.05f, origPY + 0.05f);
            Assert.InRange(reloadedFirst.SpawnCoords.Z, origPZ - 0.05f, origPZ + 0.05f);

            reloaded.CloseIO();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void PackedMvarRoundTripWithCoordinates()
    {
        // Use a file with non-trivial placement coordinates
        string path = Path.Combine(MvarDir, "h3_hardcoreConstruct.mvar");
        if (!File.Exists(path))
        {
            Console.WriteLine("SKIP: Packed .mvar test file not found");
            return;
        }

        var tempFile = Path.GetTempFileName() + ".mvar";
        File.Copy(path, tempFile, true);

        try
        {
            var original = new MccMapVariant(tempFile);
            var origPlacements = original.PlacementChunks
                .Where(c => c.TagsIndex >= 0 && c.HasPosition)
                .Select(c => (c.TagsIndex, c.SpawnCoords.X, c.SpawnCoords.Y, c.SpawnCoords.Z))
                .ToList();

            Console.WriteLine($"Original: '{original.VariantName}' with {origPlacements.Count} positioned placements");

            // Write and re-read
            original.WriteHeader();
            original.CloseIO();

            var reloaded = new MccMapVariant(tempFile);
            var reloadedPlacements = reloaded.PlacementChunks
                .Where(c => c.TagsIndex >= 0 && c.HasPosition)
                .Select(c => (c.TagsIndex, c.SpawnCoords.X, c.SpawnCoords.Y, c.SpawnCoords.Z))
                .ToList();

            Console.WriteLine($"Reloaded: '{reloaded.VariantName}' with {reloadedPlacements.Count} positioned placements");
            Assert.Equal(origPlacements.Count, reloadedPlacements.Count);

            // Check each placement's coordinates are within quantization tolerance
            int mismatches = 0;
            for (int i = 0; i < origPlacements.Count; i++)
            {
                var (oti, ox, oy, oz) = origPlacements[i];
                var (rti, rx, ry, rz) = reloadedPlacements[i];
                if (oti != rti || MathF.Abs(ox - rx) > 0.1f || MathF.Abs(oy - ry) > 0.1f || MathF.Abs(oz - rz) > 0.1f)
                {
                    Console.WriteLine($"  Mismatch [{i}]: orig=({ox:F3},{oy:F3},{oz:F3}) idx={oti} vs new=({rx:F3},{ry:F3},{rz:F3}) idx={rti}");
                    mismatches++;
                }
            }
            Console.WriteLine($"Coordinate mismatches: {mismatches}/{origPlacements.Count}");
            Assert.Equal(0, mismatches);

            reloaded.CloseIO();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private const string ReachMvarDir = "/run/media/james/m2-ssd/Program Files (x86)/Steam/steamapps/common/Halo The Master Chief Collection/haloreach/map_variants/";

    [Fact]
    public void CanLoadReachMvarFile()
    {
        string path = Path.Combine(ReachMvarDir, "hr_forgeWorld_theCage.mvar");
        if (!File.Exists(path))
        {
            Console.WriteLine("SKIP: Reach .mvar test file not found");
            return;
        }

        var blf = new BlfFile(path);
        Assert.Equal(31, blf.GetMvarMajorVersion());

        var variant = new MccReachMapVariant(blf);

        Console.WriteLine($"Variant Name: '{variant.VariantName}'");
        Console.WriteLine($"Author: '{variant.MapAuthor}'");
        Console.WriteLine($"MapId: {variant.MapId}");
        Console.WriteLine($"Map: {ReachMapDefinitions.GetMapName(variant.MapId) ?? "unknown"}");
        Console.WriteLine($"Bounds: X=[{variant.WorldBoundsXMin:F1},{variant.WorldBoundsXMax:F1}] Y=[{variant.WorldBoundsYMin:F1},{variant.WorldBoundsYMax:F1}] Z=[{variant.WorldBoundsZMin:F1},{variant.WorldBoundsZMax:F1}]");
        Console.WriteLine($"Budget: {variant.CurrentBudget}/{variant.MaximumBudget}");
        Console.WriteLine($"Objects: {variant.SpawnedObjectCount}");
        Console.WriteLine($"Quotas: {variant.TagIndex.Count(e => e.Ident >= 0)}");
        Console.WriteLine($"CanWrite: {variant.CanWrite}");

        int activePlacements = variant.PlacementChunks.Count(p => p.TagsIndex >= 0);
        Console.WriteLine($"Active Placements: {activePlacements}");

        // Basic assertions
        Assert.Equal(3006, variant.MapId); // Forge World
        Assert.True(variant.CanWrite);
        Assert.True(activePlacements > 0, "Should have active placements");
        Assert.True(variant.MaximumBudget > 0, "Budget should be positive");

        // Print first 5 placements with resolved names
        Console.WriteLine("\nFirst 5 active placements:");
        foreach (var p in variant.PlacementChunks.Where(p => p.TagsIndex >= 0).Take(5))
        {
            string name = variant.Palette?.GetVariantName(p.TagsIndex, p.VariantIndex) ?? $"Quota #{p.TagsIndex}";
            Console.WriteLine($"  [{name}] Quota#{p.TagsIndex} Var={p.VariantIndex} Pos=({p.SpawnCoords.X:F2},{p.SpawnCoords.Y:F2},{p.SpawnCoords.Z:F2})");
        }

        // Verify palette resolution
        Assert.NotNull(variant.Palette);
        Assert.Equal("Assault Rifle", variant.Palette.GetQuotaName(0));
        Assert.Equal("Weapon", variant.Palette.GetCategory(0));

        // Print all quotas that still show as "reach_object"
        Console.WriteLine("\nUnresolved quotas (reach_object):");
        for (int i = 0; i < variant.TagIndex.Count; i++)
        {
            var entry = variant.TagIndex[i];
            if (entry.Tag != null && entry.Tag.Class == "reach_object")
                Console.WriteLine($"  [{i}] {entry.Tag.Path} (min={entry.RunTimeMinimum} max={entry.RunTimeMaximum} count={entry.CountOnMap})");
        }

        // Print total quota count vs palette size
        int totalWithTags = variant.TagIndex.Count(e => e.Tag != null);
        Console.WriteLine($"\nTotal quotas with tags: {totalWithTags}");
        Console.WriteLine($"Palette size: {variant.Palette.GetQuotaName(143) ?? "null"} at index 143");
        Console.WriteLine($"Palette size: {variant.Palette.GetQuotaName(144) ?? "null"} at index 144");

        variant.CloseIO();
    }

    [Fact]
    public void DiagnoseCliffhanger()
    {
        string path = "/run/media/james/m2-ssd/Program Files (x86)/Steam/steamapps/common/Halo The Master Chief Collection/haloreach/hopper_map_variants/cliffhanger.mvar";
        if (!File.Exists(path))
        {
            Console.WriteLine("SKIP: cliffhanger.mvar not found");
            return;
        }

        var blf = new BlfFile(path);
        var variant = new MccReachMapVariant(blf);

        Console.WriteLine($"Name: '{variant.VariantName}'");
        Console.WriteLine($"MapId: {variant.MapId}");
        Console.WriteLine($"Map: {ReachMapDefinitions.GetMapName(variant.MapId) ?? "unknown"}");
        Console.WriteLine($"Palette: {(variant.Palette != null ? "loaded" : "NULL")}");

        int totalQuotas = variant.TagIndex.Count(e => e.Ident >= 0);
        Console.WriteLine($"Total quotas: {totalQuotas}");

        Console.WriteLine("\nAll quotas:");
        for (int i = 0; i < variant.TagIndex.Count; i++)
        {
            var entry = variant.TagIndex[i];
            if (entry.Tag == null || entry.Ident < 0) continue;
            Console.WriteLine($"  [{i}] class={entry.Tag.Class} path={entry.Tag.Path} count={entry.CountOnMap}");
        }

        Console.WriteLine("\nUnresolved (reach_object):");
        for (int i = 0; i < variant.TagIndex.Count; i++)
        {
            var entry = variant.TagIndex[i];
            if (entry.Tag != null && entry.Tag.Class == "reach_object")
                Console.WriteLine($"  [{i}] {entry.Tag.Path}");
        }

        variant.CloseIO();
    }

    [Fact]
    public void CanLoadAllReachMvarFiles()
    {
        if (!Directory.Exists(ReachMvarDir))
        {
            Console.WriteLine("SKIP: Reach map_variants directory not found");
            return;
        }

        var files = Directory.GetFiles(ReachMvarDir, "*.mvar");
        int loaded = 0;
        int skipped = 0;

        foreach (var file in files)
        {
            try
            {
                var blf = new BlfFile(file);
                if (blf.GetMvarMajorVersion() != 31)
                {
                    Console.WriteLine($"  SKIP (not Reach): {Path.GetFileName(file)}");
                    skipped++;
                    continue;
                }

                var variant = new MccReachMapVariant(blf);
                int activePlacements = variant.PlacementChunks.Count(p => p.TagsIndex >= 0);
                string mapName = ReachMapDefinitions.GetMapName(variant.MapId) ?? $"MapId={variant.MapId}";
                int totalQuotas = variant.TagIndex.Count(e => e.Tag != null);
                int unresolved = variant.TagIndex.Count(e => e.Tag != null && e.Tag.Class == "reach_object");
                Console.WriteLine($"  OK: {Path.GetFileName(file)} -> '{variant.VariantName}' ({mapName}) Quotas={totalQuotas} Unresolved={unresolved} Placements={activePlacements}");
                variant.CloseIO();
                loaded++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL: {Path.GetFileName(file)} -> {ex.GetType().Name}: {ex.Message}");
                Assert.Fail($"Failed to load {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nLoaded: {loaded}, Skipped: {skipped}, Total: {files.Length}");
        Assert.True(loaded > 0, "Should load at least one Reach file");
    }

    [Fact]
    public void ReachMvarRoundTrip()
    {
        if (!Directory.Exists(ReachMvarDir))
        {
            Console.WriteLine("SKIP: Reach map_variants directory not found");
            return;
        }

        // Find a Reach .mvar file to test with
        string? testFile = null;
        foreach (var f in Directory.GetFiles(ReachMvarDir, "*.mvar"))
        {
            var blf = new BlfFile(f);
            if (blf.GetMvarMajorVersion() == 31)
            {
                testFile = f;
                break;
            }
        }

        if (testFile == null)
        {
            Console.WriteLine("SKIP: No Reach .mvar files found");
            return;
        }

        Console.WriteLine($"Testing round-trip with: {Path.GetFileName(testFile)}");

        // Load original
        var origBlf = new BlfFile(testFile);
        var origVariant = new MccReachMapVariant(origBlf);

        // Save to temp file
        string tempPath = Path.Combine(Path.GetTempPath(), "forgex_roundtrip_test.mvar");
        try
        {
            // Copy original file to temp (SaveAll writes in-place via BlfFile)
            File.Copy(testFile, tempPath, true);
            var tempBlf = new BlfFile(tempPath);
            var tempVariant = new MccReachMapVariant(tempBlf);
            tempVariant.SaveAll();

            // Reload the saved file
            var savedBlf = new BlfFile(tempPath);
            var savedVariant = new MccReachMapVariant(savedBlf);

            // Compare key fields
            Assert.Equal(origVariant.VariantName, savedVariant.VariantName);
            Assert.Equal(origVariant.VariantDescription, savedVariant.VariantDescription);
            Assert.Equal(origVariant.MapAuthor, savedVariant.MapAuthor);
            Assert.Equal(origVariant.MapId, savedVariant.MapId);
            Assert.Equal(origVariant.MaximumBudget, savedVariant.MaximumBudget);
            Assert.Equal(origVariant.CurrentBudget, savedVariant.CurrentBudget);
            Assert.Equal(origVariant.WorldBoundsXMin, savedVariant.WorldBoundsXMin);
            Assert.Equal(origVariant.WorldBoundsXMax, savedVariant.WorldBoundsXMax);
            Assert.Equal(origVariant.WorldBoundsYMin, savedVariant.WorldBoundsYMin);
            Assert.Equal(origVariant.WorldBoundsYMax, savedVariant.WorldBoundsYMax);
            Assert.Equal(origVariant.WorldBoundsZMin, savedVariant.WorldBoundsZMin);
            Assert.Equal(origVariant.WorldBoundsZMax, savedVariant.WorldBoundsZMax);

            // Compare placement counts
            int origActive = origVariant.PlacementChunks.Count(p => p.TagsIndex >= 0);
            int savedActive = savedVariant.PlacementChunks.Count(p => p.TagsIndex >= 0);
            Assert.Equal(origActive, savedActive);

            // Compare individual placements
            for (int i = 0; i < origVariant.PlacementChunks.Count; i++)
            {
                var orig = origVariant.PlacementChunks[i];
                var saved = savedVariant.PlacementChunks[i];
                Assert.Equal(orig.TagsIndex, saved.TagsIndex);

                if (orig.TagsIndex < 0) continue;

                // Position: allow small float tolerance from quantization
                Assert.InRange(saved.SpawnCoords.X, orig.SpawnCoords.X - 0.1f, orig.SpawnCoords.X + 0.1f);
                Assert.InRange(saved.SpawnCoords.Y, orig.SpawnCoords.Y - 0.1f, orig.SpawnCoords.Y + 0.1f);
                Assert.InRange(saved.SpawnCoords.Z, orig.SpawnCoords.Z - 0.1f, orig.SpawnCoords.Z + 0.1f);

                Assert.Equal(orig.VariantIndex, saved.VariantIndex);
                Assert.Equal(orig.PackedFlags, saved.PackedFlags);
                Assert.Equal(orig.ObjectType, saved.ObjectType);
                Assert.Equal(orig.RespawnTime, saved.RespawnTime);
                Assert.Equal(orig.Flags, saved.Flags);
                Assert.Equal(orig.ReachTeamRaw, saved.ReachTeamRaw);
            }

            // Compare quotas
            int origQuotas = origVariant.TagIndex.Count(e => e.Ident >= 0);
            int savedQuotas = savedVariant.TagIndex.Count(e => e.Ident >= 0);
            Assert.Equal(origQuotas, savedQuotas);

            for (int i = 0; i < origVariant.TagIndex.Count; i++)
            {
                var orig = origVariant.TagIndex[i];
                var saved = savedVariant.TagIndex[i];
                if (orig.Ident < 0) continue;
                Assert.Equal(orig.RunTimeMinimum, saved.RunTimeMinimum);
                Assert.Equal(orig.RunTimeMaximum, saved.RunTimeMaximum);
                Assert.Equal(orig.CountOnMap, saved.CountOnMap);
            }

            Console.WriteLine("Round-trip test PASSED");
            Console.WriteLine($"  Name: {savedVariant.VariantName}");
            Console.WriteLine($"  Active placements: {savedActive}");
            Console.WriteLine($"  Active quotas: {savedQuotas}");

            savedVariant.CloseIO();
            tempVariant.CloseIO();
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }

        origVariant.CloseIO();
    }

    [Fact]
    public void OrientationConverterRoundTrip()
    {
        // Test with known values
        float yaw = 1.5f;
        float pitch = 0.3f;
        float roll = -0.2f;

        var (fi, fj, fk, ui, uj, uk) = OrientationConverter.ToForwardUp(yaw, pitch, roll);
        var (yaw2, pitch2, roll2) = OrientationConverter.ToYawPitchRoll(fi, fj, fk, ui, uj, uk);

        Console.WriteLine($"Original: yaw={yaw:F4} pitch={pitch:F4} roll={roll:F4}");
        Console.WriteLine($"Forward: ({fi:F4}, {fj:F4}, {fk:F4})");
        Console.WriteLine($"Up:      ({ui:F4}, {uj:F4}, {uk:F4})");
        Console.WriteLine($"Recovered: yaw={yaw2:F4} pitch={pitch2:F4} roll={roll2:F4}");

        Assert.InRange(yaw2, yaw - 0.01f, yaw + 0.01f);
        Assert.InRange(pitch2, pitch - 0.01f, pitch + 0.01f);
        Assert.InRange(roll2, roll - 0.01f, roll + 0.01f);
    }

    private const string H4MccDir = "/run/media/james/m2-ssd/Program Files (x86)/Steam/steamapps/common/Halo The Master Chief Collection/halo4/map_variants/";

    [Fact]
    public void Halo4PaletteResolvesObjectNames()
    {
        // Test with the Grifball Court .mvar (known mapId 10245)
        string path = Path.Combine(H4MccDir, "grifballcourt.mvar");
        if (!File.Exists(path))
        {
            Console.WriteLine("SKIP: H4 MCC .mvar not found");
            return;
        }

        var blf = new BlfFile(path);
        var variant = new MccHalo4MapVariant(blf);

        Console.WriteLine($"MapId: {variant.MapId}, Name: {variant.VariantName}");
        Assert.Equal(10245, variant.MapId);
        Assert.NotNull(variant.Palette);

        // All maps share the same first 31 weapons and 8 armor abilities
        Assert.Equal("Magnum", variant.Palette.GetQuotaName(0));
        Assert.Equal("Weapon", variant.Palette.GetCategory(0));
        Assert.Equal("Assault Rifle", variant.Palette.GetQuotaName(1));
        Assert.Equal("Battle Rifle", variant.Palette.GetQuotaName(2));
        Assert.Equal("Armor Ability", variant.Palette.GetCategory(31)); // Jetpack
        Assert.Equal("Gadget", variant.Palette.GetCategory(39));        // Explosives

        // Grifball has only 90 entries (no Dominion, fewer vehicles)
        Assert.Equal("Vehicle", variant.Palette.GetCategory(72));       // Mongoose (at 72, not 82)

        // Verify most entries are resolved (some may be beyond palette size)
        int unresolvedCount = 0;
        for (int i = 0; i < variant.TagIndex.Count; i++)
        {
            var entry = variant.TagIndex[i];
            if (entry.Tag != null && entry.Tag.Class == "h4_object")
            {
                unresolvedCount++;
                Console.WriteLine($"  Unresolved [{i}]: {entry.Tag.Path} (min={entry.RunTimeMinimum} max={entry.RunTimeMaximum} count={entry.CountOnMap})");
            }
        }
        Console.WriteLine($"Unresolved h4_object entries: {unresolvedCount}");
        // Allow a few entries beyond palette size to remain unresolved
        Assert.True(unresolvedCount <= 5, $"Too many unresolved h4_object entries: {unresolvedCount}");

        // Print first 10 active placements with resolved names
        Console.WriteLine("\nFirst 10 active placements:");
        foreach (var p in variant.PlacementChunks.Where(p => p.TagsIndex >= 0).Take(10))
        {
            string name = variant.Palette.GetVariantName(p.TagsIndex, p.VariantIndex);
            Console.WriteLine($"  [{name}] Quota#{p.TagsIndex} Var={p.VariantIndex}");
        }

        // Print all unique categories found
        var categories = variant.TagIndex
            .Where(e => e.Tag != null)
            .Select(e => e.Tag!.Class)
            .Distinct()
            .OrderBy(c => c);
        Console.WriteLine($"\nCategories: {string.Join(", ", categories)}");
    }

    [Fact]
    public void Halo4ForgeCanvasPaletteResolution()
    {
        // Test all forge canvas .mvar files that exist
        var canvasFiles = new[] { "ca_forge_erosion_ascent.mvar", "ca_forge_bonanza_relay.mvar", "ca_forge_ravine_settler.mvar" };
        bool anyTested = false;

        foreach (var file in canvasFiles)
        {
            string path = Path.Combine(H4MccDir, file);
            if (!File.Exists(path)) continue;

            var blf = new BlfFile(path);
            var variant = new MccHalo4MapVariant(blf);
            Console.WriteLine($"\n{file}: MapId={variant.MapId}, Name={variant.VariantName}");

            if (variant.Palette == null)
            {
                Console.WriteLine($"  No palette for mapId {variant.MapId}");
                continue;
            }

            anyTested = true;

            // Common entries should resolve
            Assert.Equal("Magnum", variant.Palette.GetQuotaName(0));
            Assert.Equal("Weapon", variant.Palette.GetCategory(0));

            // Print all categories and first 5 placements
            var categories = variant.TagIndex
                .Where(e => e.Tag != null)
                .Select(e => e.Tag!.Class)
                .Distinct()
                .OrderBy(c => c);
            Console.WriteLine($"  Categories: {string.Join(", ", categories)}");

            foreach (var p in variant.PlacementChunks.Where(p => p.TagsIndex >= 0).Take(5))
            {
                string name = variant.Palette.GetVariantName(p.TagsIndex, p.VariantIndex);
                Console.WriteLine($"  [{name}] Quota#{p.TagsIndex} Var={p.VariantIndex}");
            }
        }

        if (!anyTested)
            Console.WriteLine("SKIP: No H4 MCC forge canvas .mvar files found");
    }
}
