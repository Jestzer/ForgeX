namespace ForgeX.Core.Halo3;

/// <summary>
/// Common interface for map variant data, implemented by both Xbox 360 (MapVariant)
/// and MCC (MccMapVariant) readers.
/// </summary>
public interface IMapVariantData
{
    string VariantName { get; set; }
    string VariantDescription { get; set; }
    string MapAuthor { get; set; }
    int MapId { get; set; }
    byte SpawnedObjectCount { get; set; }

    float WorldBoundsXMin { get; set; }
    float WorldBoundsXMax { get; set; }
    float WorldBoundsYMin { get; set; }
    float WorldBoundsYMax { get; set; }
    float WorldBoundsZMin { get; set; }
    float WorldBoundsZMax { get; set; }

    float MaximumBudget { get; set; }
    float CurrentBudget { get; set; }

    List<TagIndexEntry> TagIndex { get; }
    List<PlacementChunk> PlacementChunks { get; }
    TagDatabase? Tags { get; }

    bool CanWrite { get; }

    void WriteHeader();
    void WritePlacement(PlacementChunk chunk);
    void WriteTagIndexEntry(TagIndexEntry entry);
    TagIndexEntry? FindTagIndexEntry(string tagClass, string tagPath, int tagsIndex);
    void CloseIO();
}
