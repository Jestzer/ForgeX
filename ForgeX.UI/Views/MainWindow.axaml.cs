using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ForgeX.UI.ViewModels;

namespace ForgeX.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Halo 3 Usermap",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Halo 3 Usermaps (.mvar)") { Patterns = new[] { "*.mvar" } },
                new FilePickerFileType("Xbox 360 Usermaps") { Patterns = new[] { "*" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (files.Count > 0)
        {
            var path = files[0].TryGetLocalPath();
            if (path != null && DataContext is MainWindowViewModel vm)
            {
                vm.OpenFile(path);
            }
        }
    }

    private void OnTagTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not TreeView treeView) return;
        if (DataContext is not MainWindowViewModel vm) return;

        if (treeView.SelectedItem is TagClassNode node && node.IsLeaf)
        {
            vm.TagBrowser.SelectTagEntry(node.TagClass, node.TagPath, node.TagsIndex);
        }
    }
}
