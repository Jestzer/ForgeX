using CommunityToolkit.Mvvm.ComponentModel;
using ForgeX.Core.Halo3;
using ForgeX.Core.Halo4;
using ForgeX.Core.Reach;

namespace ForgeX.UI.ViewModels;

public partial class MapHeaderViewModel : ViewModelBase
{
    [ObservableProperty] private string _variantName = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _author = "";
    [ObservableProperty] private string _mapName = "";
    [ObservableProperty] private int _mapId;
    [ObservableProperty] private int _spawnedObjectCount;
    [ObservableProperty] private float _maximumBudget;
    [ObservableProperty] private float _currentBudget;

    public void LoadFrom(IMapVariantData variant)
    {
        VariantName = variant.VariantName;
        Description = variant.VariantDescription;
        Author = variant.MapAuthor;
        MapId = variant.MapId;
        SpawnedObjectCount = variant.SpawnedObjectCount;
        MaximumBudget = variant.MaximumBudget;
        CurrentBudget = variant.CurrentBudget;
        MapName = variant.Tags?.MapName
            ?? Halo4MapDefinitions.GetMapName(variant.MapId)
            ?? ReachMapDefinitions.GetMapName(variant.MapId)
            ?? $"Unknown ({variant.MapId})";
    }

    public void SaveTo(IMapVariantData variant)
    {
        variant.VariantName = VariantName;
        variant.VariantDescription = Description;
        variant.MapAuthor = Author;
        variant.MaximumBudget = MaximumBudget;
        variant.CurrentBudget = CurrentBudget;
    }
}
