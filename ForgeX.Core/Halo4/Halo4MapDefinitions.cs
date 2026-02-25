namespace ForgeX.Core.Halo4;

/// <summary>
/// Maps Halo 4 map IDs to display names.
/// </summary>
public static class Halo4MapDefinitions
{
    private static readonly Dictionary<int, string> MapIdToName = new()
    {
        // Forge canvases
        { 7000, "Erosion" },
        { 7100, "Impact" },
        { 7200, "Ravine" },
        { 7300, "Forge Island" },

        // Arena / War Games maps
        { 10010, "Adrift" },
        { 10020, "Abandon" },
        { 10040, "Complex" },
        { 10050, "Exile" },
        { 10060, "Haven" },
        { 10070, "Longbow" },
        { 10080, "Meltdown" },
        { 10090, "Ragnarok" },
        { 10100, "Solace" },
        { 10110, "Vortex" },
        { 10245, "Grifball Court" },
        { 10255, "Relay" },
        { 10256, "Settler" },

        // Crimson Map Pack
        { 11050, "Harvest" },
        { 11060, "Shatter" },
        { 11070, "Wreckage" },

        // Majestic Map Pack
        { 11080, "Landfall" },
        { 11090, "Monolith" },
        { 11100, "Skyline" },

        // Castle Map Pack
        { 11110, "Daybreak" },
        { 11120, "Outcast" },
        { 11130, "Perdition" },

        // Champions Bundle
        { 11140, "Pitfall" },
        { 11150, "Vertigo" },
    };

    public static string? GetMapName(int mapId)
    {
        return MapIdToName.TryGetValue(mapId, out var name) ? name : null;
    }
}
