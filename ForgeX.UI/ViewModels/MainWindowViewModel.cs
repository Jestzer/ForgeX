using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ForgeX.Core.Blf;
using ForgeX.Core.Halo3;
using ForgeX.Core.Xbox360;
using ForgeX.UI.Services;

namespace ForgeX.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private MapHeaderViewModel _mapHeader = new();
    [ObservableProperty] private TagBrowserViewModel _tagBrowser = new();
    [ObservableProperty] private string _statusMessage = "Open a usermap file to begin.";
    [ObservableProperty] private bool _isFileLoaded;
    [ObservableProperty] private string _windowTitle = "ForgeX - Halo 3 Forge Usermap Editor";
    [ObservableProperty] private bool _isXbox360Format;
    [ObservableProperty] private ObservableCollection<RecentFileItem> _recentFiles = new();

    private StfsContainer? _container;
    private IMapVariantData? _variant;
    private string? _currentFilePath;

    public MainWindowViewModel()
    {
        LoadRecentFiles();
    }

    public void OpenFile(string filePath)
    {
        try
        {
            // Close any existing file
            CloseCurrentFile();

            _currentFilePath = filePath;

            // Detect format by reading first 4 bytes
            var magic = new byte[4];
            using (var fs = File.OpenRead(filePath))
                fs.Read(magic, 0, 4);

            string magicStr = System.Text.Encoding.ASCII.GetString(magic);

            if (magicStr.StartsWith("CON") || magicStr.StartsWith("LIV") || magicStr.StartsWith("PIR"))
            {
                // Xbox 360 STFS container
                _container = new StfsContainer(filePath);
                _variant = new MapVariant(_container);
                IsXbox360Format = true;
            }
            else if (magicStr == "_blf")
            {
                // MCC BLF container (.mvar file)
                _variant = new MccMapVariant(filePath);
                IsXbox360Format = false;
            }
            else
            {
                throw new InvalidDataException(
                    $"Unrecognized file format (magic: 0x{magic[0]:X2}{magic[1]:X2}{magic[2]:X2}{magic[3]:X2}).");
            }

            MapHeader.LoadFrom(_variant);
            TagBrowser.Load(_variant);

            IsFileLoaded = true;
            string formatLabel = IsXbox360Format ? "Xbox 360" : "MCC";
            StatusMessage = $"Loaded ({formatLabel}): {Path.GetFileName(filePath)}";
            if (_variant is MccMapVariant mcc && mcc.DecompressedPath != null)
                StatusMessage += $" — Decompressed copy: {Path.GetFileName(mcc.DecompressedPath)}";
            WindowTitle = $"ForgeX - {_variant.VariantName} ({MapHeader.MapName})";

            // Add to recent files
            RecentFilesService.Add(filePath);
            LoadRecentFiles();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsFileLoaded = false;
        }
    }

    [RelayCommand]
    private void OpenRecentFile(string filePath)
    {
        if (File.Exists(filePath))
            OpenFile(filePath);
        else
            StatusMessage = $"File not found: {Path.GetFileName(filePath)}";
    }

    [RelayCommand]
    private void SaveHeader()
    {
        if (_variant == null || !_variant.CanWrite) return;

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
        IsXbox360Format = false;
        MapHeader = new MapHeaderViewModel();
        TagBrowser = new TagBrowserViewModel();
    }

    private void LoadRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var path in RecentFilesService.Load())
            RecentFiles.Add(new RecentFileItem(path));
    }
}

public class RecentFileItem
{
    public string FilePath { get; }
    public string DisplayName { get; }

    public RecentFileItem(string filePath)
    {
        FilePath = filePath;
        DisplayName = Path.GetFileName(filePath);
    }
}
