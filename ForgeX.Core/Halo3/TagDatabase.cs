using System.Xml;

namespace ForgeX.Core.Halo3;

/// <summary>
/// Loads and queries tag definitions from XML data for a specific Halo 3 map.
/// This is a pure data class with no UI dependencies.
/// </summary>
public class TagDatabase
{
    private readonly List<Tag> _tags = new();

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

    private void ParseXml(string xmlData)
    {
        using var reader = XmlReader.Create(new StringReader(xmlData));
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.Name != "Map")
                continue;

            int tagCount = Convert.ToInt32(reader.GetAttribute("TagCount"));
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "Tag")
                {
                    var tag = new Tag
                    {
                        Class = reader.GetAttribute("Class") ?? string.Empty,
                        Path = reader.GetAttribute("Path") ?? string.Empty,
                        Ident = Convert.ToInt32(reader.GetAttribute("Ident"))
                    };
                    _tags.Add(tag);
                }
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
}
