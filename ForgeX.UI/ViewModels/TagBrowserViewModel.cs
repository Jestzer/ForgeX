using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ForgeX.Core.Blf;
using ForgeX.Core.Halo3;
using ForgeX.Core.Halo4;
using ForgeX.Core.Reach;

namespace ForgeX.UI.ViewModels;

public partial class TagBrowserViewModel : ViewModelBase
{
    private IMapVariantData? _variant;
    private IPaletteDatabase? _palette;
    private bool _isLoadingSelection;

    /// <summary>
    /// Callback invoked when any edit is made that should mark the file as dirty.
    /// Set by MainWindowViewModel.
    /// </summary>
    public Action? MarkDirty { get; set; }

    // Property names that represent user edits (for dirty tracking)
    private static readonly HashSet<string> EditProperties = new()
    {
        nameof(Ident), nameof(RuntimeMin), nameof(RuntimeMax),
        nameof(CountOnMap), nameof(DesignTimeMax), nameof(Cost),
        nameof(SelectedTagClass), nameof(SelectedTagPath),
        nameof(PlacementTagsIndex),
        nameof(PlacementX), nameof(PlacementY), nameof(PlacementZ),
        nameof(PlacementYaw), nameof(PlacementPitch), nameof(PlacementRoll),
        nameof(PlacementRespawnTime), nameof(PlacementTeam), nameof(PlacementSpareClips),
        nameof(PlacementHideAtStart), nameof(PlacementSymmetric), nameof(PlacementAsymmetric),
        nameof(PlacementGameSpecific), nameof(PlacementPhysicsMode),
        nameof(PlacementChunkType)
    };

    // TreeView data
    [ObservableProperty] private ObservableCollection<TagClassNode> _tagTree = new();
    private List<TagClassNode>? _fullClassNodes; // unfiltered class nodes for search

    // Search
    [ObservableProperty] private string _searchText = "";

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
    [ObservableProperty] private bool _canWrite;
    [ObservableProperty] private bool _isMcc;
    [ObservableProperty] private bool _isXbox360H3;
    [ObservableProperty] private bool _hasMccPhysics;
    [ObservableProperty] private bool _hasBypassLimit;
    [ObservableProperty] private string _bypassLimitLabel = "Forge Limit Bypass: Disabled";

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
    [ObservableProperty] private bool _placementHideAtStart;
    [ObservableProperty] private bool _placementSymmetric;
    [ObservableProperty] private bool _placementAsymmetric;
    [ObservableProperty] private bool _placementGameSpecific;
    [ObservableProperty] private int _placementPhysicsMode;
    [ObservableProperty] private string _placementChunkType = "";
    [ObservableProperty] private ObservableCollection<string> _chunkTypes = new()
    {
        "Added", "Edited", "null", "Original", "PlayerSpawn", "Reserved"
    };

    public TagBrowserViewModel()
    {
        PropertyChanged += OnEditPropertyChanged;
    }

    private void OnEditPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingSelection) return;
        if (e.PropertyName != null && EditProperties.Contains(e.PropertyName))
            MarkDirty?.Invoke();
    }

    public void Load(IMapVariantData variant)
    {
        _isLoadingSelection = true;
        try
        {
            _variant = variant;
            _palette = (IPaletteDatabase?)(variant as MccReachMapVariant)?.Palette
                    ?? (variant as MccHalo4MapVariant)?.Palette;
            CanWrite = variant.CanWrite;
            IsMcc = variant is MccMapVariant or MccReachMapVariant or MccHalo4MapVariant;
            TagTree.Clear();

            // Build tree: group tag index entries by class
            var addedPaths = new HashSet<string>();
            var classNodes = new Dictionary<string, TagClassNode>();

            int entryCount = Math.Min(variant.TagIndex.Count, 256);
            for (int i = 0; i < entryCount; i++)
            {
                var entry = variant.TagIndex[i];

                // Create synthetic Tag for unmatched entries so they appear in the tree
                if (entry.Tag == null)
                {
                    if (entry.Ident == 0 || entry.Ident == -1) continue;
                    entry.Tag = new Tag
                    {
                        Class = "unknown",
                        Path = $"0x{(uint)entry.Ident:X8}",
                        Ident = entry.Ident,
                        TagsIndex = i
                    };
                }

                if (!classNodes.TryGetValue(entry.Tag.Class, out var classNode))
                {
                    classNode = new TagClassNode { ClassName = entry.Tag.Class };
                    classNodes[entry.Tag.Class] = classNode;
                }

                string displayName = GetTagDisplayName(entry);
                string key = $"{entry.Tag.Class}/{entry.Tag.Path}/{entry.Tag.TagsIndex}";
                if (!addedPaths.Contains(key))
                {
                    classNode.Children.Add(new TagClassNode
                    {
                        ClassName = displayName,
                        IsLeaf = true,
                        TagPath = entry.Tag.Path,
                        TagsIndex = entry.Tag.TagsIndex,
                        TagClass = entry.Tag.Class
                    });
                    addedPaths.Add(key);
                }
            }

            if (classNodes.Count == 0) return;

            // Add tag class nodes directly (no root wrapper — map name is in the header)
            foreach (var cn in classNodes.Values.OrderBy(c => c.ClassName))
                TagTree.Add(cn);
            _fullClassNodes = classNodes.Values.OrderBy(c => c.ClassName).ToList();

            // Load tag class combo box
            TagClasses.Clear();
            if (variant.Tags != null)
            {
                foreach (var cls in variant.Tags.GetDistinctClasses())
                    TagClasses.Add(cls);
            }
            else
            {
                foreach (var cls in classNodes.Keys.OrderBy(c => c))
                    TagClasses.Add(cls);
            }
        }
        finally
        {
            _isLoadingSelection = false;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        if (_fullClassNodes == null) return;

        TagTree.Clear();
        if (string.IsNullOrWhiteSpace(value))
        {
            foreach (var cn in _fullClassNodes)
                TagTree.Add(cn);
            return;
        }

        var filter = value.Trim();
        foreach (var classNode in _fullClassNodes)
        {
            bool classMatches = classNode.ClassName.Contains(filter, StringComparison.OrdinalIgnoreCase);

            var matchingChildren = new List<TagClassNode>();
            foreach (var leaf in classNode.Children)
            {
                if (classMatches ||
                    leaf.ClassName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    matchingChildren.Add(leaf);
                }
            }

            if (matchingChildren.Count > 0)
            {
                var filteredClass = new TagClassNode
                {
                    ClassName = classNode.ClassName
                };
                foreach (var child in matchingChildren)
                    filteredClass.Children.Add(child);
                TagTree.Add(filteredClass);
            }
        }
    }

    partial void OnSelectedTagClassChanged(string? value)
    {
        TagPaths.Clear();
        if (value == null || _variant == null) return;

        if (_variant.Tags != null)
        {
            foreach (var tag in _variant.Tags.GetTagsByClass(value))
                TagPaths.Add(tag.Path);
        }
        else
        {
            // Reach: build paths from TagIndex entries
            foreach (var entry in _variant.TagIndex.Where(e => e.Tag != null && e.Tag.Class == value))
                TagPaths.Add(entry.Tag!.Path);
        }
    }

    partial void OnSelectedTagPathChanged(string? value)
    {
        if (value == null || _variant == null || SelectedTagClass == null) return;

        if (_variant.Tags != null)
        {
            var tag = _variant.Tags.FindTag(SelectedTagClass, value);
            if (tag != null)
                Ident = tag.Ident.ToString();
        }
        else
        {
            // Reach: find from TagIndex
            var entry = _variant.TagIndex.FirstOrDefault(e =>
                e.Tag != null && e.Tag.Class == SelectedTagClass && e.Tag.Path == value);
            if (entry?.Tag != null)
                Ident = entry.Tag.Ident.ToString();
        }
    }

    public void SelectTagEntry(string tagClass, string tagPath, int tagsIndex)
    {
        // Apply any pending edits from the previously selected tag/placement
        ApplyTagEdits();
        ApplyPlacementEdits();

        var entry = _variant?.FindTagIndexEntry(tagClass, tagPath, tagsIndex);
        if (entry == null) return;

        _isLoadingSelection = true;
        try
        {
            SelectedEntry = entry;
            IsTagSelected = true;

            Ident = entry.Ident.ToString();
            HasBypassLimit = IsXbox360H3 && (entry.Ident & 0x10000000) != 0;
            BypassLimitLabel = HasBypassLimit
                ? "Forge Limit Bypass: Enabled" : "Forge Limit Bypass: Disabled";
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
                Placements.Add(GetPlacementLabel(i, entry.PlacedItems[i]));

            if (Placements.Count > 0)
                SelectedPlacementIndex = 0;
        }
        finally
        {
            _isLoadingSelection = false;
        }
    }

    partial void OnSelectedPlacementIndexChanged(int oldValue, int newValue)
    {
        // Apply edits from the previously selected placement before loading the new one
        if (!_isLoadingSelection && oldValue >= 0 && SelectedEntry != null &&
            oldValue < SelectedEntry.PlacedItems.Count)
        {
            ApplyPlacementEdits(oldValue);
        }

        if (newValue < 0 || SelectedEntry == null || newValue >= SelectedEntry.PlacedItems.Count)
        {
            HasSelectedPlacement = false;
            return;
        }

        _isLoadingSelection = true;
        try
        {
            HasSelectedPlacement = true;
            var chunk = SelectedEntry.PlacedItems[newValue];
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
            PlacementHideAtStart = chunk.HideAtStart;
            PlacementSymmetric = chunk.Symmetric;
            PlacementAsymmetric = chunk.Asymmetric;
            PlacementGameSpecific = chunk.GameSpecific;
            PlacementPhysicsMode = chunk.PhysicsMode;
            PlacementChunkType = chunk.ChunkType.ToString();
        }
        finally
        {
            _isLoadingSelection = false;
        }
    }

    /// <summary>
    /// Applies all pending UI edits to in-memory model objects.
    /// Called by MainWindowViewModel before SaveAll().
    /// </summary>
    public void ApplyAllPendingEdits()
    {
        ApplyTagEdits();
        ApplyPlacementEdits();
    }

    private void ApplyTagEdits()
    {
        if (SelectedEntry == null) return;

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
    }

    private void ApplyPlacementEdits()
    {
        ApplyPlacementEdits(SelectedPlacementIndex);
    }

    private void ApplyPlacementEdits(int placementIndex)
    {
        if (SelectedEntry == null || placementIndex < 0) return;
        if (placementIndex >= SelectedEntry.PlacedItems.Count) return;

        var chunk = SelectedEntry.PlacedItems[placementIndex];

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
        chunk.HideAtStart = PlacementHideAtStart;
        chunk.Symmetric = PlacementSymmetric;
        chunk.Asymmetric = PlacementAsymmetric;
        chunk.GameSpecific = PlacementGameSpecific;
        chunk.PhysicsMode = (byte)PlacementPhysicsMode;
        chunk.ChunkType = ParseChunkType(PlacementChunkType);
    }

    [RelayCommand]
    private void AddPlacement()
    {
        if (SelectedEntry == null || _variant == null || !_variant.CanWrite) return;

        // Find first empty placement slot
        int slotIndex = -1;
        for (int i = 0; i < _variant.PlacementChunks.Count; i++)
        {
            if (_variant.PlacementChunks[i].TagsIndex == -1)
            {
                slotIndex = i;
                break;
            }
        }
        if (slotIndex == -1) return; // All 640 slots full

        int tagIndex = _variant.TagIndex.IndexOf(SelectedEntry);
        if (tagIndex < 0) return;

        // Reuse the empty slot (it already has the correct Offset for file writes)
        var chunk = _variant.PlacementChunks[slotIndex];
        chunk.ChunkType = ChunkType.Added;
        chunk.TagsIndex = tagIndex;
        chunk.SpawnCoords = new SpawnCoords();
        chunk.Flags = 0;
        chunk.Team = 0;
        chunk.SpareClips = 0;
        chunk.RespawnTime = 0;
        chunk.Entry = SelectedEntry;

        SelectedEntry.PlacedItems.Add(chunk);
        SelectedEntry.CountOnMap = (byte)SelectedEntry.PlacedItems.Count;

        MarkDirty?.Invoke();

        // Refresh UI
        _isLoadingSelection = true;
        try
        {
            Placements.Clear();
            for (int i = 0; i < SelectedEntry.PlacedItems.Count; i++)
                Placements.Add(GetPlacementLabel(i, SelectedEntry.PlacedItems[i]));
            CountOnMap = SelectedEntry.CountOnMap.ToString();

            // Select the newly added placement
            SelectedPlacementIndex = SelectedEntry.PlacedItems.Count - 1;
        }
        finally
        {
            _isLoadingSelection = false;
        }
    }

    [RelayCommand]
    private void DeletePlacement()
    {
        if (SelectedEntry == null || _variant == null || !_variant.CanWrite || SelectedPlacementIndex < 0) return;
        if (SelectedPlacementIndex >= SelectedEntry.PlacedItems.Count) return;

        var chunk = SelectedEntry.PlacedItems[SelectedPlacementIndex];
        chunk.TagsIndex = -1;

        SelectedEntry.PlacedItems.RemoveAt(SelectedPlacementIndex);
        SelectedEntry.CountOnMap = (byte)SelectedEntry.PlacedItems.Count;

        MarkDirty?.Invoke();

        // Refresh
        int deletedIndex = SelectedPlacementIndex;
        _isLoadingSelection = true;
        try
        {
            Placements.Clear();
            for (int i = 0; i < SelectedEntry.PlacedItems.Count; i++)
                Placements.Add(GetPlacementLabel(i, SelectedEntry.PlacedItems[i]));
            CountOnMap = SelectedEntry.CountOnMap.ToString();

            // Auto-select next available placement
            if (SelectedEntry.PlacedItems.Count > 0)
                SelectedPlacementIndex = Math.Min(deletedIndex, SelectedEntry.PlacedItems.Count - 1);
        }
        finally
        {
            _isLoadingSelection = false;
        }
    }

    [RelayCommand]
    private void BypassLimit()
    {
        if (SelectedEntry == null || _variant == null || !IsXbox360H3) return;

        int newIdent = SelectedEntry.Ident ^ 0x10000000;

        // Check if another tag entry already has the target ident
        var targetEntry = _variant.TagIndex.FirstOrDefault(e =>
            e != SelectedEntry && e.Ident == newIdent);

        TagIndexEntry entryToSelect;

        if (targetEntry != null)
        {
            // Merge: move all placements from current entry to the target entry
            int targetIndex = _variant.TagIndex.IndexOf(targetEntry);
            foreach (var chunk in SelectedEntry.PlacedItems)
            {
                chunk.TagsIndex = targetIndex;
                chunk.Entry = targetEntry;
                targetEntry.PlacedItems.Add(chunk);
            }
            targetEntry.CountOnMap = (byte)targetEntry.PlacedItems.Count;

            // Clear the old entry
            SelectedEntry.PlacedItems.Clear();
            SelectedEntry.CountOnMap = 0;
            SelectedEntry.Ident = 0;

            entryToSelect = targetEntry;
        }
        else
        {
            // No merge needed — just toggle the ident
            SelectedEntry.Ident = newIdent;

            // Re-resolve the tag name from the database with the new ident
            if (_variant.Tags != null)
            {
                var resolved = _variant.Tags.FindTag(newIdent);
                if (resolved != null)
                    SelectedEntry.Tag = resolved;
            }

            entryToSelect = SelectedEntry;
        }

        RebuildTree();
        MarkDirty?.Invoke();
        LoadEntryIntoUI(entryToSelect);
    }

    /// <summary>
    /// Directly loads a TagIndexEntry's data into all UI fields without going through FindTagIndexEntry.
    /// Used after operations like bypass toggle that change idents and rebuild the tree.
    /// </summary>
    private void LoadEntryIntoUI(TagIndexEntry entry)
    {
        _isLoadingSelection = true;
        try
        {
            SelectedEntry = entry;
            IsTagSelected = true;

            Ident = entry.Ident.ToString();
            HasBypassLimit = IsXbox360H3 && (entry.Ident & 0x10000000) != 0;
            BypassLimitLabel = HasBypassLimit
                ? "Forge Limit Bypass: Enabled" : "Forge Limit Bypass: Disabled";
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

            Placements.Clear();
            for (int i = 0; i < entry.PlacedItems.Count; i++)
                Placements.Add(GetPlacementLabel(i, entry.PlacedItems[i]));

            if (Placements.Count > 0)
                SelectedPlacementIndex = 0;
            else
                HasSelectedPlacement = false;
        }
        finally
        {
            _isLoadingSelection = false;
        }
    }

    private void RebuildTree()
    {
        if (_variant == null) return;

        _isLoadingSelection = true;
        try
        {
            // Rebuild the full tree from scratch
            var addedPaths = new HashSet<string>();
            var classNodes = new Dictionary<string, TagClassNode>();

            int entryCount = Math.Min(_variant.TagIndex.Count, 256);
            for (int i = 0; i < entryCount; i++)
            {
                var entry = _variant.TagIndex[i];
                if (entry.Tag == null)
                {
                    if (entry.Ident == 0 || entry.Ident == -1) continue;
                    entry.Tag = new Tag
                    {
                        Class = "unknown",
                        Path = $"0x{(uint)entry.Ident:X8}",
                        Ident = entry.Ident,
                        TagsIndex = i
                    };
                }

                if (!classNodes.TryGetValue(entry.Tag.Class, out var classNode))
                {
                    classNode = new TagClassNode { ClassName = entry.Tag.Class };
                    classNodes[entry.Tag.Class] = classNode;
                }

                string displayName = GetTagDisplayName(entry);
                string key = $"{entry.Tag.Class}/{entry.Tag.Path}/{entry.Tag.TagsIndex}";
                if (!addedPaths.Contains(key))
                {
                    classNode.Children.Add(new TagClassNode
                    {
                        ClassName = displayName,
                        IsLeaf = true,
                        TagPath = entry.Tag.Path,
                        TagsIndex = entry.Tag.TagsIndex,
                        TagClass = entry.Tag.Class
                    });
                    addedPaths.Add(key);
                }
            }

            _fullClassNodes = classNodes.Values.OrderBy(c => c.ClassName).ToList();
            SearchText = "";
            TagTree.Clear();
            foreach (var cn in _fullClassNodes)
                TagTree.Add(cn);
        }
        finally
        {
            _isLoadingSelection = false;
        }
    }

    private string GetTagDisplayName(TagIndexEntry entry)
    {
        if (entry.Tag == null) return "";
        string name = entry.Tag.Path;
        if (IsXbox360H3 && (entry.Ident & 0x10000000) != 0)
            name += " (bypassed)";
        return name;
    }

    private string GetPlacementLabel(int index, PlacementChunk chunk)
    {
        if (_palette != null && chunk.VariantIndex >= 0 && chunk.TagsIndex >= 0)
        {
            var varName = _palette.GetVariantName(chunk.TagsIndex, chunk.VariantIndex);
            if (varName != null)
                return $"{index}: {varName}";
        }
        return $"Placement Chunk: {index}";
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
