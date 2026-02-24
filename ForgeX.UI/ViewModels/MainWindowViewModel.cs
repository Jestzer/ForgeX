using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ForgeX.Core.Blf;
using ForgeX.Core.Halo3;
using ForgeX.Core.Reach;
using ForgeX.Core.Xbox360;
using ForgeX.UI.Services;

namespace ForgeX.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private MapHeaderViewModel _mapHeader = new();
    [ObservableProperty] private TagBrowserViewModel _tagBrowser = new();
    [ObservableProperty] private string _statusMessage = "Open a usermap file to begin.";
    [ObservableProperty] private bool _isFileLoaded;
    [ObservableProperty] private string _windowTitle = "ForgeX - Halo 3 / Reach Forge Usermap Editor";
    [ObservableProperty] private bool _isXbox360Format;
    [ObservableProperty] private ObservableCollection<RecentFileItem> _recentFiles = new();
    [ObservableProperty] private bool _hasUnsavedChanges;

    private StfsContainer? _container;
    private IMapVariantData? _variant;
    private string? _currentFilePath;
    private string? _reachStfsTempPath; // Temp BLF file for Reach Xbox 360 STFS containers

    /// <summary>
    /// Set by the View to show error dialogs. Parameters: title, message.
    /// </summary>
    public Action<string, string>? ShowError { get; set; }

    /// <summary>
    /// Set by the View to show a confirmation dialog.
    /// Returns true (Save), false (Discard), or null (Cancel).
    /// </summary>
    public Func<string, string, Task<bool?>>? ShowConfirmDialog { get; set; }

    /// <summary>
    /// Set by the View to show the About dialog.
    /// </summary>
    public Action? ShowAboutDialog { get; set; }

    public MainWindowViewModel()
    {
        LoadRecentFiles();
    }

    partial void OnHasUnsavedChangesChanged(bool value)
    {
        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        if (_variant == null)
        {
            WindowTitle = "ForgeX - Halo 3 / Reach Forge Usermap Editor";
            return;
        }
        string dirty = HasUnsavedChanges ? " *" : "";
        WindowTitle = $"ForgeX - {_variant.VariantName} ({MapHeader.MapName}){dirty}";
    }

    public async Task OpenFileAsync(string filePath)
    {
        if (!await ConfirmDiscardChanges()) return;

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

                var sandboxEntry = _container.GetEntryByFileName("sandbox.map")
                    ?? throw new InvalidDataException("Container does not contain a sandbox.map file.");
                var sandboxData = sandboxEntry.GetData();

                // Detect Reach vs H3 by checking if sandbox.map is a BLF file
                bool isReachBlf = sandboxData.Length >= 4 &&
                    sandboxData[0] == 0x5F && sandboxData[1] == 0x62 &&
                    sandboxData[2] == 0x6C && sandboxData[3] == 0x66; // "_blf"

                if (isReachBlf)
                {
                    // Reach Xbox 360: sandbox.map is a BLF file with packed mvar bitstream
                    _reachStfsTempPath = Path.GetTempFileName() + ".mvar";
                    File.WriteAllBytes(_reachStfsTempPath, sandboxData);
                    var blf = new BlfFile(_reachStfsTempPath);
                    _variant = new MccReachMapVariant(blf);
                }
                else
                {
                    // Halo 3 Xbox 360: sandbox.map is raw binary structs
                    _variant = new MapVariant(_container);
                }

                IsXbox360Format = true;
            }
            else if (magicStr == "_blf")
            {
                // MCC BLF container (.mvar file) — detect H3 vs Reach by mvar version
                var blf = new BlfFile(filePath);
                short mvarVersion = blf.GetMvarMajorVersion();
                if (mvarVersion == 31)
                    _variant = new MccReachMapVariant(blf);
                else
                    _variant = new MccMapVariant(blf);
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
            string formatLabel = IsXbox360Format
                ? (_variant is MccReachMapVariant ? "Xbox 360 Reach" : "Xbox 360")
                : _variant is MccReachMapVariant ? "MCC Reach" : "MCC";
            StatusMessage = $"Loaded ({formatLabel}): {Path.GetFileName(filePath)}";
            if (_variant is MccMapVariant mcc && mcc.DecompressedPath != null)
                StatusMessage += $" — Decompressed copy: {Path.GetFileName(mcc.DecompressedPath)}";

            HasUnsavedChanges = false;

            // Wire dirty tracking after loading (so initial loads don't mark dirty)
            TagBrowser.MarkDirty = () => HasUnsavedChanges = true;
            MapHeader.PropertyChanged += (_, _) =>
            {
                if (IsFileLoaded) HasUnsavedChanges = true;
            };

            UpdateWindowTitle();

            // Add to recent files
            RecentFilesService.Add(filePath);
            LoadRecentFiles();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsFileLoaded = false;
            ShowError?.Invoke("Error Opening File", ex.Message);
        }
    }

    [RelayCommand]
    private async Task OpenRecentFile(string filePath)
    {
        if (File.Exists(filePath))
            await OpenFileAsync(filePath);
        else
            StatusMessage = $"File not found: {Path.GetFileName(filePath)}";
    }

    [RelayCommand]
    private void Save()
    {
        if (_variant == null || !_variant.CanWrite) return;

        try
        {
            // Apply header edits from MapHeaderViewModel to IMapVariantData
            MapHeader.SaveTo(_variant);

            // Apply any pending tag/placement edits from UI fields to in-memory model
            TagBrowser.ApplyAllPendingEdits();

            // Write everything to disk in one operation
            _variant.SaveAll();

            // For Reach Xbox 360: flush the updated BLF temp file back to the STFS container
            if (_container != null && _reachStfsTempPath != null)
            {
                var sandboxEntry = _container.GetEntryByFileName("sandbox.map");
                if (sandboxEntry != null)
                    sandboxEntry.WriteData(File.ReadAllBytes(_reachStfsTempPath));
            }

            // Re-sign Xbox 360 STFS containers after saving
            if (_container != null)
            {
                _container.Resign();
            }

            HasUnsavedChanges = false;
            StatusMessage = _container != null ? "File saved and re-signed." : "File saved.";
            UpdateWindowTitle();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving: {ex.Message}";
            ShowError?.Invoke("Error Saving File", ex.Message);
        }
    }

    [RelayCommand]
    private void ShowAbout()
    {
        ShowAboutDialog?.Invoke();
    }

    [RelayCommand]
    private async Task CloseFile()
    {
        if (!await ConfirmDiscardChanges()) return;

        CloseCurrentFile();
        StatusMessage = "File closed.";
        WindowTitle = "ForgeX - Halo 3 / Reach Forge Usermap Editor";
    }

    /// <summary>
    /// Returns true if it is safe to proceed (no unsaved changes, or user chose to save/discard).
    /// Returns false if the user cancelled.
    /// </summary>
    public async Task<bool> ConfirmDiscardChanges()
    {
        if (!HasUnsavedChanges) return true;
        if (ShowConfirmDialog == null) return true;

        var result = await ShowConfirmDialog("Unsaved Changes",
            "You have unsaved changes. Do you want to save before continuing?");

        if (result == null) return false; // Cancel
        if (result == true) Save();       // Save, then proceed
        return true;                      // Discard or saved — proceed
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
        if (_reachStfsTempPath != null)
        {
            try { File.Delete(_reachStfsTempPath); } catch { }
            _reachStfsTempPath = null;
        }
        IsFileLoaded = false;
        IsXbox360Format = false;
        HasUnsavedChanges = false;
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
