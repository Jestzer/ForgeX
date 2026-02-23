namespace ForgeX.Core.Halo3;

/// <summary>
/// Loads embedded XML tag definition files for each supported Halo 3 map.
/// Resource names use Halo 3's internal codenames.
/// </summary>
public static class MapDefinitions
{
    // Map IDs to internal Halo 3 codenames (matching embedded XML filenames)
    private static readonly Dictionary<int, string> MapIdToResource = new()
    {
        { 30, "zanzibar" },         // Last Resort / Zanzibar
        { 300, "construct" },       // Construct
        { 310, "deadlock" },        // High Ground (internal: deadlock)
        { 320, "guardian" },        // Guardian
        { 330, "isolation" },       // Isolation
        { 340, "riverworld" },      // Valhalla (internal: riverworld)
        { 350, "salvation" },       // Epitaph (internal: salvation)
        { 360, "snowbound" },       // Snowbound
        { 380, "chill" },           // Narrows (internal: chill)
        { 390, "shrine" },          // The Pit (internal: shrine)
        { 400, "sidewinder" },      // Sandtrap (internal: sidewinder)
        { 410, "bunkerworld" },     // Standoff (internal: bunkerworld)
        { 470, "cyberdyne" },       // Avalanche (internal: cyberdyne)
        { 480, "warehouse" },       // Foundry (internal: warehouse)
        { 520, "lockout" },         // Blackout (internal: lockout)
        { 580, "armory" },          // Rats Nest (internal: armory)
        { 590, "ghosttown" },       // Ghost Town
        { 600, "chillout" }         // Cold Storage (internal: chillout)
    };

    private static readonly Dictionary<int, string> MapIdToName = new()
    {
        { 30, "Zanzibar" },
        { 300, "Construct" },
        { 310, "High Ground" },
        { 320, "Guardian" },
        { 330, "Isolation" },
        { 340, "Valhalla" },
        { 350, "Epitaph" },
        { 360, "Snowbound" },
        { 380, "Narrows" },
        { 390, "The Pit" },
        { 400, "Sandtrap" },
        { 410, "Standoff" },
        { 470, "Avalanche" },
        { 480, "Foundry" },
        { 520, "Blackout" },
        { 580, "Rats Nest" },
        { 590, "Ghost Town" },
        { 600, "Cold Storage" }
    };

    public static string? GetMapXml(int mapId)
    {
        if (!MapIdToResource.TryGetValue(mapId, out var resourceName))
            return null;

        var assembly = typeof(MapDefinitions).Assembly;
        var fullName = $"ForgeX.Core.Resources.Maps.{resourceName}.xml";
        using var stream = assembly.GetManifestResourceStream(fullName);
        if (stream == null)
            return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string? GetMapName(int mapId)
    {
        return MapIdToName.TryGetValue(mapId, out var name) ? name : null;
    }

    public static IReadOnlyList<int> GetSupportedMapIds()
    {
        return MapIdToResource.Keys.ToList();
    }
}
