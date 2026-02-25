namespace ForgeX.Core.Halo4;

/// <summary>
/// Maps Halo 4 map IDs to display names.
/// </summary>
public static class Halo4MapDefinitions
{
    private static readonly Dictionary<int, string> MapIdToName = new()
    {
        // MCC forge canvases
        { 7000, "Erosion" },
        { 7100, "Impact" },
        { 7200, "Ravine" },
        { 7300, "Forge Island" },

        // MCC arena / War Games maps
        { 10010, "Adrift" },
        { 10020, "Abandon" },
        { 10040, "Complex" },
        { 10050, "Exile" },
        { 10060, "Haven" },
        { 10070, "Longbow" },
        { 10080, "Meltdown" },       // Xbox 360: Haven (10080)
        { 10090, "Ragnarok" },
        { 10100, "Solace" },
        { 10110, "Vortex" },
        { 10245, "Grifball Court" },  // Xbox 360: Erosion (10245)
        { 10255, "Relay" },           // Xbox 360: Impact (10255)
        { 10256, "Settler" },         // Xbox 360: Ravine (10256)

        // MCC Crimson Map Pack
        { 11050, "Harvest" },
        { 11060, "Shatter" },
        { 11070, "Wreckage" },

        // MCC Majestic Map Pack
        { 11080, "Landfall" },
        { 11090, "Monolith" },
        { 11100, "Skyline" },

        // MCC Castle Map Pack
        { 11110, "Daybreak" },
        { 11120, "Outcast" },
        { 11130, "Perdition" },

        // MCC Champions Bundle
        { 11140, "Pitfall" },
        { 11150, "Vertigo" },

        // Xbox 360 arena / War Games maps (IDs differ from MCC)
        { 10085, "Complex" },
        { 10091, "Ragnarok" },
        { 10102, "Harvest" },
        { 10200, "Longbow" },
        { 10202, "Solace" },
        { 10210, "Adrift" },
        { 10225, "Abandon" },
        { 10226, "Exile" },
        { 10252, "Vortex" },
        { 10261, "Meltdown" },

        // Xbox 360 DLC maps
        { 13110, "Landfall" },
        { 13120, "Perdition" },
        { 13130, "Daybreak" },
        { 13131, "Monolith" },
        { 13140, "Outcast" },
        { 13160, "Skyline" },
        { 13301, "Wreckage" },
        { 13302, "Shatter" },
        { 14100, "Forge Island" },
        { 15000, "Pitfall" },
        { 15010, "Vertigo" },
    };

    public static string? GetMapName(int mapId)
    {
        return MapIdToName.TryGetValue(mapId, out var name) ? name : null;
    }
}
