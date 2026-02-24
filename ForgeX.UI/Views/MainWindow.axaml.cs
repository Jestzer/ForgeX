using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ForgeX.UI.ViewModels;

namespace ForgeX.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.ShowError = ShowErrorDialog;
                vm.ShowConfirmDialog = ShowConfirmDialogAsync;
                RefreshRecentFilesMenu();
            }
        };

        Closing += OnWindowClosing;
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (!vm.HasUnsavedChanges) return;

        // Cancel the close, show the dialog, then re-close if confirmed
        e.Cancel = true;

        if (await vm.ConfirmDiscardChanges())
        {
            // Detach handler to prevent re-entry, then close
            Closing -= OnWindowClosing;
            Close();
        }
    }

    private async Task<bool?> ShowConfirmDialogAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource<bool?>();

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(45, 43, 43))
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 16
        };

        panel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            FontSize = 14
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 12
        };

        var saveBtn = new Button
        {
            Content = "Save",
            Padding = new Avalonia.Thickness(24, 6),
            Background = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(30, 105, 155)),
            BorderThickness = new Avalonia.Thickness(2)
        };
        saveBtn.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };

        var discardBtn = new Button
        {
            Content = "Discard",
            Padding = new Avalonia.Thickness(24, 6),
            Background = new SolidColorBrush(Color.FromRgb(90, 45, 45)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(139, 0, 0)),
            BorderThickness = new Avalonia.Thickness(2)
        };
        discardBtn.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };

        var cancelBtn = new Button
        {
            Content = "Cancel",
            Padding = new Avalonia.Thickness(24, 6),
            Background = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            BorderThickness = new Avalonia.Thickness(2)
        };
        cancelBtn.Click += (_, _) => { tcs.TrySetResult(null); dialog.Close(); };

        // Handle dialog closed via X button as Cancel
        dialog.Closed += (_, _) => tcs.TrySetResult(null);

        buttons.Children.Add(saveBtn);
        buttons.Children.Add(discardBtn);
        buttons.Children.Add(cancelBtn);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        await dialog.ShowDialog(this);
        return await tcs.Task;
    }

    private async void ShowErrorDialog(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(45, 43, 43))
        };

        var panel = new DockPanel
        {
            Margin = new Avalonia.Thickness(20)
        };

        var button = new Button
        {
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Avalonia.Thickness(24, 6),
            Background = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(30, 105, 155)),
            BorderThickness = new Avalonia.Thickness(2)
        };
        button.Click += (_, _) => dialog.Close();
        DockPanel.SetDock(button, Dock.Bottom);
        panel.Children.Add(button);

        panel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        dialog.Content = panel;
        await dialog.ShowDialog(this);
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
                await vm.OpenFileAsync(path);
                RefreshRecentFilesMenu();
            }
        }
    }

    private void RefreshRecentFilesMenu()
    {
        if (DataContext is not MainWindowViewModel vm) return;

        RecentFilesMenu.Items.Clear();

        if (vm.RecentFiles.Count == 0)
        {
            var emptyItem = new MenuItem { Header = "(No recent files)", IsEnabled = false };
            RecentFilesMenu.Items.Add(emptyItem);
            return;
        }

        foreach (var recent in vm.RecentFiles)
        {
            var item = new MenuItem { Header = recent.DisplayName, Tag = recent.FilePath };
            item.Click += OnRecentFileClick;
            RecentFilesMenu.Items.Add(item);
        }
    }

    private void OnRecentFileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string filePath && DataContext is MainWindowViewModel vm)
        {
            // Close the entire File menu so the user sees the main window immediately
            FileMenu.Close();
            vm.OpenRecentFileCommand.Execute(filePath);
            RefreshRecentFilesMenu();
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
