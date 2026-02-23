using System.Text.Json;

namespace ForgeX.UI.Services;

/// <summary>
/// Persists a list of recently opened file paths to disk.
/// Stored as JSON in ~/.config/ForgeX/recent.json (Linux/Mac) or %APPDATA%/ForgeX/recent.json (Windows).
/// </summary>
public static class RecentFilesService
{
    private const int MaxRecentFiles = 10;

    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ForgeX");

    private static string FilePath => Path.Combine(ConfigDir, "recent.json");

    public static List<string> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new List<string>();

            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static void Add(string filePath)
    {
        var list = Load();

        // Remove if already present (will re-add at top)
        list.RemoveAll(p => string.Equals(p, filePath, StringComparison.OrdinalIgnoreCase));

        // Insert at beginning
        list.Insert(0, filePath);

        // Trim to max
        if (list.Count > MaxRecentFiles)
            list.RemoveRange(MaxRecentFiles, list.Count - MaxRecentFiles);

        Save(list);
    }

    private static void Save(List<string> list)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Silently ignore write failures (permissions, disk full, etc.)
        }
    }
}
