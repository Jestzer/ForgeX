using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ForgeX.Core.Halo3;

namespace ForgeX.UI.ViewModels;

public partial class TagBrowserViewModel : ViewModelBase
{
    private IMapVariantData? _variant;

    // TreeView data
    [ObservableProperty] private ObservableCollection<TagClassNode> _tagTree = new();

    // Tag class/path combo boxes
    [ObservableProperty] private ObservableCollection<string> _tagClasses = new();
    [ObservableProperty] private string? _selectedTagClass;
    [ObservableProperty] private ObservableCollection<string> _tagPaths = new();
    [ObservableProperty] private string? _selectedTagPath;

    // Selected tag editing fields
    [ObservableProperty] private TagIndexEntry? _selectedEntry;
    [ObservableProperty] private string _ident = "";
    [ObservableProperty] private string _runtimeMin = "";
    [ObservableProperty] private string _runtimeMax = "";
    [ObservableProperty] private string _countOnMap = "";
    [ObservableProperty] private string _designTimeMax = "";
    [ObservableProperty] private string _cost = "";
    [ObservableProperty] private bool _isTagSelected;

    // Placement list for selected tag
    [ObservableProperty] private ObservableCollection<string> _placements = new();
    [ObservableProperty] private int _selectedPlacementIndex = -1;
    [ObservableProperty] private bool _hasSelectedPlacement;

    // Placement editing fields
    [ObservableProperty] private string _placementTagsIndex = "";
    [ObservableProperty] private string _placementX = "";
    [ObservableProperty] private string _placementY = "";
    [ObservableProperty] private string _placementZ = "";
    [ObservableProperty] private string _placementYaw = "";
    [ObservableProperty] private string _placementPitch = "";
    [ObservableProperty] private string _placementRoll = "";
    [ObservableProperty] private string _placementRespawnTime = "";
    [ObservableProperty] private string _placementTeam = "";
    [ObservableProperty] private string _placementSpareClips = "";
    [ObservableProperty] private bool _placementFlag1;
    [ObservableProperty] private bool _placementFlag2;
    [ObservableProperty] private bool _placementFlag3;
    [ObservableProperty] private string _placementChunkType = "";
    [ObservableProperty] private ObservableCollection<string> _chunkTypes = new()
    {
        "Added", "Edited", "null", "Original", "PlayerSpawn", "Reserved"
    };

    public void Load(IMapVariantData variant)
    {
        _variant = variant;
        TagTree.Clear();

        if (variant.Tags == null) return;

        // Build tree: group tag index entries by class
        var addedPaths = new HashSet<string>();
        var classNodes = new Dictionary<string, TagClassNode>();

        for (int i = 0; i < 256; i++)
        {
            var entry = variant.TagIndex[i];
            if (entry.Tag == null) continue;

            if (!classNodes.TryGetValue(entry.Tag.Class, out var classNode))
            {
                classNode = new TagClassNode { ClassName = entry.Tag.Class };
                classNodes[entry.Tag.Class] = classNode;
            }

            string key = $"{entry.Tag.Class}/{entry.Tag.Path}/{entry.Tag.TagsIndex}";
            if (!addedPaths.Contains(key))
            {
                classNode.Children.Add(new TagClassNode
                {
                    ClassName = entry.Tag.Path,
                    IsLeaf = true,
                    TagPath = entry.Tag.Path,
                    TagsIndex = entry.Tag.TagsIndex,
                    TagClass = entry.Tag.Class
                });
                addedPaths.Add(key);
            }
        }

        // Add root node for map name
        var root = new TagClassNode
        {
            ClassName = variant.Tags.MapName ?? "Map",
            IsRoot = true
        };
        foreach (var cn in classNodes.Values.OrderBy(c => c.ClassName))
            root.Children.Add(cn);
        TagTree.Add(root);

        // Load tag class combo box
        TagClasses.Clear();
        foreach (var cls in variant.Tags.GetDistinctClasses())
            TagClasses.Add(cls);
    }

    partial void OnSelectedTagClassChanged(string? value)
    {
        TagPaths.Clear();
        if (value == null || _variant?.Tags == null) return;

        foreach (var tag in _variant.Tags.GetTagsByClass(value))
            TagPaths.Add(tag.Path);
    }

    partial void OnSelectedTagPathChanged(string? value)
    {
        if (value == null || _variant?.Tags == null || SelectedTagClass == null) return;

        var tag = _variant.Tags.FindTag(SelectedTagClass, value);
        if (tag != null)
            Ident = tag.Ident.ToString();
    }

    public void SelectTagEntry(string tagClass, string tagPath, int tagsIndex)
    {
        var entry = _variant?.FindTagIndexEntry(tagClass, tagPath, tagsIndex);
        if (entry == null) return;

        SelectedEntry = entry;
        IsTagSelected = true;

        Ident = entry.Ident.ToString();
        RuntimeMin = entry.RunTimeMinimum.ToString();
        RuntimeMax = entry.RunTimeMaximum.ToString();
        CountOnMap = entry.CountOnMap.ToString();
        DesignTimeMax = entry.DesignTimeMaximum.ToString();
        Cost = entry.Cost.ToString();

        if (entry.Tag != null)
        {
            SelectedTagClass = entry.Tag.Class;
            SelectedTagPath = entry.Tag.Path;
        }

        // Load placements for this tag
        Placements.Clear();
        for (int i = 0; i < entry.PlacedItems.Count; i++)
            Placements.Add($"Placement Chunk: {i}");

        if (Placements.Count > 0)
            SelectedPlacementIndex = 0;
    }

    partial void OnSelectedPlacementIndexChanged(int value)
    {
        if (value < 0 || SelectedEntry == null || value >= SelectedEntry.PlacedItems.Count)
        {
            HasSelectedPlacement = false;
            return;
        }

        HasSelectedPlacement = true;
        var chunk = SelectedEntry.PlacedItems[value];
        PlacementTagsIndex = chunk.TagsIndex.ToString();
        PlacementX = chunk.SpawnCoords.X.ToString();
        PlacementY = chunk.SpawnCoords.Y.ToString();
        PlacementZ = chunk.SpawnCoords.Z.ToString();
        PlacementYaw = chunk.SpawnCoords.Yaw.ToString();
        PlacementPitch = chunk.SpawnCoords.Pitch.ToString();
        PlacementRoll = chunk.SpawnCoords.Roll.ToString();
        PlacementRespawnTime = chunk.RespawnTime.ToString();
        PlacementTeam = chunk.Team.ToString();
        PlacementSpareClips = chunk.SpareClips.ToString();
        PlacementFlag1 = chunk.Flag1;
        PlacementFlag2 = chunk.Flag2;
        PlacementFlag3 = chunk.Flag3;
        PlacementChunkType = chunk.ChunkType.ToString();
    }

    [RelayCommand]
    private void SaveTag()
    {
        if (SelectedEntry == null || _variant == null) return;

        if (int.TryParse(Ident, out int ident)) SelectedEntry.Ident = ident;
        if (byte.TryParse(RuntimeMin, out byte rtMin)) SelectedEntry.RunTimeMinimum = rtMin;
        if (byte.TryParse(RuntimeMax, out byte rtMax)) SelectedEntry.RunTimeMaximum = rtMax;
        if (byte.TryParse(CountOnMap, out byte count)) SelectedEntry.CountOnMap = count;
        if (byte.TryParse(DesignTimeMax, out byte dtMax)) SelectedEntry.DesignTimeMaximum = dtMax;
        if (float.TryParse(Cost, out float cost)) SelectedEntry.Cost = cost;

        if (SelectedEntry.Tag != null)
        {
            if (SelectedTagPath != null) SelectedEntry.Tag.Path = SelectedTagPath;
            if (SelectedTagClass != null) SelectedEntry.Tag.Class = SelectedTagClass;
        }

        _variant.WriteTagIndexEntry(SelectedEntry);
    }

    [RelayCommand]
    private void SavePlacement()
    {
        if (SelectedEntry == null || _variant == null || SelectedPlacementIndex < 0) return;
        if (SelectedPlacementIndex >= SelectedEntry.PlacedItems.Count) return;

        var chunk = SelectedEntry.PlacedItems[SelectedPlacementIndex];

        if (int.TryParse(PlacementTagsIndex, out int ti)) chunk.TagsIndex = ti;
        if (float.TryParse(PlacementX, out float x)) chunk.SpawnCoords.X = x;
        if (float.TryParse(PlacementY, out float y)) chunk.SpawnCoords.Y = y;
        if (float.TryParse(PlacementZ, out float z)) chunk.SpawnCoords.Z = z;
        if (float.TryParse(PlacementYaw, out float yaw)) chunk.SpawnCoords.Yaw = yaw;
        if (float.TryParse(PlacementPitch, out float pitch)) chunk.SpawnCoords.Pitch = pitch;
        if (float.TryParse(PlacementRoll, out float roll)) chunk.SpawnCoords.Roll = roll;
        if (byte.TryParse(PlacementRespawnTime, out byte rt)) chunk.RespawnTime = rt;
        if (byte.TryParse(PlacementTeam, out byte team)) chunk.Team = team;
        if (byte.TryParse(PlacementSpareClips, out byte sc)) chunk.SpareClips = sc;
        chunk.Flag1 = PlacementFlag1;
        chunk.Flag2 = PlacementFlag2;
        chunk.Flag3 = PlacementFlag3;
        chunk.ChunkType = ParseChunkType(PlacementChunkType);

        _variant.WritePlacement(chunk);
    }

    [RelayCommand]
    private void DeletePlacement()
    {
        if (SelectedEntry == null || _variant == null || SelectedPlacementIndex < 0) return;
        if (SelectedPlacementIndex >= SelectedEntry.PlacedItems.Count) return;

        var chunk = SelectedEntry.PlacedItems[SelectedPlacementIndex];
        chunk.TagsIndex = -1;
        _variant.WritePlacement(chunk);

        SelectedEntry.PlacedItems.RemoveAt(SelectedPlacementIndex);
        SelectedEntry.CountOnMap = (byte)SelectedEntry.PlacedItems.Count;
        _variant.WriteTagIndexEntry(SelectedEntry);

        // Refresh
        Placements.Clear();
        for (int i = 0; i < SelectedEntry.PlacedItems.Count; i++)
            Placements.Add($"Placement Chunk: {i}");
        CountOnMap = SelectedEntry.CountOnMap.ToString();
    }

    private static ChunkType ParseChunkType(string text) => text switch
    {
        "Added" => ChunkType.Added,
        "Edited" => ChunkType.Edited,
        "Original" => ChunkType.Original,
        "PlayerSpawn" => ChunkType.PlayerSpawn,
        "Reserved" => ChunkType.Reserved,
        _ => ChunkType.Null,
    };
}

public class TagClassNode
{
    public string ClassName { get; set; } = "";
    public bool IsRoot { get; set; }
    public bool IsLeaf { get; set; }
    public ObservableCollection<TagClassNode> Children { get; set; } = new();

    // Leaf node properties (when IsLeaf = true, this represents a single tag)
    public string TagPath { get; set; } = "";
    public string TagClass { get; set; } = "";
    public int TagsIndex { get; set; }
}

// Keep TagNode for selection handling in code-behind
public class TagNode
{
    public string TagPath { get; set; } = "";
    public string TagClass { get; set; } = "";
    public int TagsIndex { get; set; }
}
