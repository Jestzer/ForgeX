using ForgeX.Core.Blf;
using ForgeX.Core.Halo3;
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
}
