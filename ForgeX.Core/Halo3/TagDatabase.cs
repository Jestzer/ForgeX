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

    public TagDatabase(int mapId)
    {
        string? xml = MapDefinitions.GetMapXml(mapId);
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
        for (int i = 0; i < _tags.Count; i++)
        {
            if (_tags[i].Ident == ident)
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
