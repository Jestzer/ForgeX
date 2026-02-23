using ForgeX.Core.Blf;
using ForgeX.Core.Halo3;
using ForgeX.Core.IO;

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
            Assert.Equal(121, activeTagEntries);

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
        int skipped = 0;

        foreach (var file in files)
        {
            var tempFile = Path.GetTempFileName() + ".mvar";
            File.Copy(file, tempFile, true);

            try
            {
                var variant = new MccMapVariant(tempFile);
                int activePlacements = variant.PlacementChunks.Count(c => c.TagsIndex >= 0);
                Console.WriteLine($"  OK: {Path.GetFileName(file)} -> '{variant.VariantName}' MapId={variant.MapId} Placements={activePlacements}");
                variant.CloseIO();
                loaded++;
            }
            catch (NotSupportedException ex) when (ex.Message.Contains("Compressed"))
            {
                Console.WriteLine($"  SKIP (compressed): {Path.GetFileName(file)}");
                skipped++;
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

        Console.WriteLine($"\nLoaded: {loaded}, Skipped: {skipped}, Total: {files.Length}");
        Assert.True(loaded > 0, "Should load at least one file");
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
}
