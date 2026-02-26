using System.Xml;

namespace ForgeX.Core.Halo3;

/// <summary>
/// Loads and queries tag definitions from XML data for a specific Halo 3 map.
/// This is a pure data class with no UI dependencies.
/// </summary>
public class TagDatabase
{
    private readonly List<Tag> _tags = new();
    // Palette: category index (0=Vehicle,1=Weapon,...) → list of (ident, name) entries
    private readonly Dictionary<int, List<PaletteEntry>> _palettes = new();

    public IReadOnlyList<Tag> AllTags => _tags.AsReadOnly();
    public int TagCount => _tags.Count;
    public string? MapName { get; private set; }

    public bool IsXbox360 { get; }

    public TagDatabase(int mapId, bool xbox360 = false)
    {
        IsXbox360 = xbox360;

        // For Xbox 360, try the X360-specific database first, fall back to MCC
        string? xml = xbox360 ? MapDefinitions.GetMapXml(mapId, xbox360: true) : null;
        xml ??= MapDefinitions.GetMapXml(mapId);
        if (xml != null)
            ParseXml(xml);

        MapName = MapDefinitions.GetMapName(mapId);
    }

    public TagDatabase(string xmlData)
    {
        ParseXml(xmlData);
    }

    private static readonly string[] PaletteCategoryNames =
        { "Vehicle", "Weapon", "Equipment", "Scenery", "Teleporter", "Goal", "Spawner" };

    private void ParseXml(string xmlData)
    {
        using var reader = XmlReader.Create(new StringReader(xmlData));
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
                continue;

            if (reader.Name == "Tag")
            {
                var tag = new Tag
                {
                    Class = reader.GetAttribute("Class") ?? string.Empty,
                    Path = reader.GetAttribute("Path") ?? string.Empty,
                    Ident = Convert.ToInt32(reader.GetAttribute("Ident"))
                };
                _tags.Add(tag);
            }
            else if (reader.Name == "Palette")
            {
                string category = reader.GetAttribute("Category") ?? string.Empty;
                int catIndex = Array.IndexOf(PaletteCategoryNames, category);
                if (catIndex < 0) continue;

                var entries = new List<PaletteEntry>();
                using var paletteReader = reader.ReadSubtree();
                while (paletteReader.Read())
                {
                    if (paletteReader.NodeType == XmlNodeType.Element && paletteReader.Name == "Entry")
                    {
                        entries.Add(new PaletteEntry
                        {
                            Ident = Convert.ToInt32(paletteReader.GetAttribute("Ident")),
                            Name = paletteReader.GetAttribute("Name") ?? string.Empty
                        });
                    }
                }
                _palettes[catIndex] = entries;
            }
        }
    }

    public IReadOnlyList<string> GetDistinctClasses()
    {
        return _tags
            .Where(t => !string.IsNullOrEmpty(t.Class))
            .Select(t => t.Class)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }

    public IReadOnlyList<Tag> GetTagsByClass(string tagClass)
    {
        return _tags
            .Where(t => t.Class == tagClass)
            .OrderBy(t => t.Path)
            .ToList();
    }

    public Tag? FindTag(int ident)
    {
        // 1. Exact match in this map's database
        for (int i = 0; i < _tags.Count; i++)
        {
            if (_tags[i].Ident == ident)
                return _tags[i];
        }

        if (ident == 0 || ident == -1)
            return null;

        // 2. Xbox 360 "BypassLimit" toggle: bit 28 is flipped (0xE... ↔ 0xF...).
        //    Try the original ident with bit 28 toggled.
        int toggled = ident ^ 0x10000000;
        for (int i = 0; i < _tags.Count; i++)
        {
            if (_tags[i].Ident == toggled)
                return _tags[i];
        }

        // 3. Exact match across ALL map databases (catches cross-map injected objects)
        var crossMap = CrossMapIndex.FindTag(ident, IsXbox360);
        if (crossMap != null)
            return crossMap;

        // 4. Cross-map with bit-28 toggle
        crossMap = CrossMapIndex.FindTag(toggled, IsXbox360);
        if (crossMap != null)
            return crossMap;

        // 5. Datum-index fallback: match by low 16 bits only (handles title update salt drift).
        //    Only search this map's database — cross-map datum matching would be unreliable.
        int datum = ident & 0xFFFF;
        for (int i = 0; i < _tags.Count; i++)
        {
            if ((_tags[i].Ident & 0xFFFF) == datum)
                return _tags[i];
        }

        return null;
    }

    public Tag? FindTag(string tagClass, string path)
    {
        for (int i = 0; i < _tags.Count; i++)
        {
            if (_tags[i].Class == tagClass && _tags[i].Path == path)
                return _tags[i];
        }
        return null;
    }

    /// <summary>
    /// Looks up a tag by MCC forge palette index.
    /// Palette indices encode (type &lt;&lt; 16) | slot, where type = category + 1.
    /// </summary>
    public Tag? FindTagByPaletteIndex(int paletteIndex)
    {
        int type = (paletteIndex >> 16) & 0xFFFF;
        int slot = paletteIndex & 0xFFFF;
        int catIndex = type - 1; // mvar type 1=Vehicle(cat 0), 2=Weapon(cat 1), etc.

        if (!_palettes.TryGetValue(catIndex, out var entries))
            return null;
        if (slot < 0 || slot >= entries.Count)
            return null;

        var paletteEntry = entries[slot];

        // Look up the full tag using the palette entry's real ident
        var tag = FindTag(paletteEntry.Ident);
        if (tag != null)
            return tag;

        // Tag ident not in the tag table — return a tag with the palette display name
        return new Tag
        {
            Ident = paletteEntry.Ident,
            Path = paletteEntry.Name,
            Class = PaletteCategoryNames[catIndex].ToLowerInvariant()
        };
    }

    public bool HasPalettes => _palettes.Count > 0;
}

public class PaletteEntry
{
    public int Ident { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Lazy-loaded index of all tags across every supported Halo 3 map.
/// Maintains separate MCC and Xbox 360 indexes.
/// Used to resolve cross-map injected objects (e.g. elephants on Sandtrap).
/// </summary>
internal static class CrossMapIndex
{
    private static Dictionary<int, Tag>? _mccIndex;
    private static Dictionary<int, Tag>? _x360Index;

    public static Tag? FindTag(int ident, bool xbox360)
    {
        if (xbox360)
        {
            _x360Index ??= BuildIndex(xbox360: true);
            return _x360Index.TryGetValue(ident, out var tag) ? tag : null;
        }

        _mccIndex ??= BuildIndex(xbox360: false);
        return _mccIndex.TryGetValue(ident, out var tag2) ? tag2 : null;
    }

    private static Dictionary<int, Tag> BuildIndex(bool xbox360)
    {
        var index = new Dictionary<int, Tag>();
        foreach (int mapId in MapDefinitions.GetSupportedMapIds())
        {
            string? xml = MapDefinitions.GetMapXml(mapId, xbox360);
            if (xml == null) continue;

            var db = new TagDatabase(xml);
            foreach (var tag in db.AllTags)
            {
                index.TryAdd(tag.Ident, tag);
            }
        }
        return index;
    }
}
