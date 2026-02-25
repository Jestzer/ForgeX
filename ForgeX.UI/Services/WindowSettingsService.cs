using System.Text.Json;

namespace ForgeX.UI.Services;

/// <summary>
/// Persists window size to disk so it can be restored on next launch.
/// Stored as JSON in ~/.config/ForgeX/window.json (Linux/Mac) or %APPDATA%/ForgeX/window.json (Windows).
/// </summary>
public static class WindowSettingsService
{
    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ForgeX");

    private static string FilePath => Path.Combine(ConfigDir, "window.json");

    public static WindowSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new WindowSettings();

            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<WindowSettings>(json) ?? new WindowSettings();
        }
        catch
        {
            return new WindowSettings();
        }
    }

    public static void Save(WindowSettings settings)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Silently ignore write failures
        }
    }
}

public class WindowSettings
{
    public double Width { get; set; }
    public double Height { get; set; }
}
