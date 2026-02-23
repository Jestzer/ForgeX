using ForgeX.Core.Halo3;
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
}
