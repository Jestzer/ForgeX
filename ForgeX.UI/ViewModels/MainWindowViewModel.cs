using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ForgeX.Core.Halo3;
using ForgeX.Core.Xbox360;

namespace ForgeX.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private MapHeaderViewModel _mapHeader = new();
    [ObservableProperty] private TagBrowserViewModel _tagBrowser = new();
    [ObservableProperty] private string _statusMessage = "Open a usermap file to begin.";
    [ObservableProperty] private bool _isFileLoaded;
    [ObservableProperty] private string _windowTitle = "ForgeX - Halo 3 Forge Usermap Editor";

    private StfsContainer? _container;
    private MapVariant? _variant;
    private string? _currentFilePath;

    public void OpenFile(string filePath)
    {
        try
        {
            // Close any existing file
            CloseCurrentFile();

            _currentFilePath = filePath;
            _container = new StfsContainer(filePath);
            _variant = new MapVariant(_container);

            MapHeader.LoadFrom(_variant);
            TagBrowser.Load(_variant);

            IsFileLoaded = true;
            StatusMessage = $"Loaded: {Path.GetFileName(filePath)}";
            WindowTitle = $"ForgeX - {_variant.VariantName} ({MapHeader.MapName})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsFileLoaded = false;
        }
    }

    [RelayCommand]
    private void SaveHeader()
    {
        if (_variant == null) return;

        try
        {
            MapHeader.SaveTo(_variant);
            _variant.WriteHeader();
            StatusMessage = "Header saved.";
            WindowTitle = $"ForgeX - {_variant.VariantName} ({MapHeader.MapName})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving header: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ResignContainer()
    {
        if (_container == null) return;

        try
        {
            _container.Resign();
            StatusMessage = "Container re-signed successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error re-signing: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CloseFile()
    {
        CloseCurrentFile();
        StatusMessage = "File closed.";
        WindowTitle = "ForgeX - Halo 3 Forge Usermap Editor";
    }

    private void CloseCurrentFile()
    {
        if (_variant != null)
        {
            try { _variant.CloseIO(); } catch { }
            _variant = null;
        }
        _container = null;
        _currentFilePath = null;
        IsFileLoaded = false;
        MapHeader = new MapHeaderViewModel();
        TagBrowser = new TagBrowserViewModel();
    }
}
