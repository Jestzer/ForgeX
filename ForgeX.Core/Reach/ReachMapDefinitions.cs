namespace ForgeX.Core.Reach;

/// <summary>
/// Maps Halo: Reach map IDs to display names.
/// No tag database is needed since Reach quotas lack object_definition_index.
/// </summary>
public static class ReachMapDefinitions
{
    private static readonly Dictionary<int, string> MapIdToName = new()
    {
        // Reach DLC / Anniversary maps (IDs verified against MCC .mvar files)
        { 10010, "Damnation Anniversary" },
        { 10020, "Beaver Creek Anniversary" },
        { 10030, "Timberland Anniversary" },
        { 10050, "Headlong Anniversary" },
        { 10060, "Hang 'em High Anniversary" },
        { 10070, "Prisoner Anniversary" },

        // Reach multiplayer maps
        { 1000, "Boardwalk" },
        { 1020, "Boneyard" },
        { 1035, "Countdown" },
        { 1040, "Powerhouse" },
        { 1055, "Reflection" },
        { 1080, "Spire" },
        { 1150, "Sword Base" },
        { 1200, "Zealot" },
        { 1500, "Anchor 9" },
        { 1510, "Breakpoint" },
        { 1520, "Tempest" },

        // Reach DLC maps
        { 2001, "Condemned" },
        { 2002, "Highlands" },
        { 2004, "Battle Canyon" },
        { 2005, "Penance" },
        { 2006, "Ridgeline" },
        { 2007, "Solitary" },
        { 2008, "High Noon" },
        { 2009, "Breakneck" },

        // Forge World
        { 3006, "Forge World" },
    };

    public static string? GetMapName(int mapId)
    {
        return MapIdToName.TryGetValue(mapId, out var name) ? name : null;
    }
}
