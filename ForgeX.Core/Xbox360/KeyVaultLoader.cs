namespace ForgeX.Core.Xbox360;

/// <summary>
/// Loads the Xbox 360 keyvault file (KV.bin) from disk.
/// The KV.bin must be located alongside the executable.
/// </summary>
public static class KeyVaultLoader
{
    public static byte[] Load()
    {
        var kvPath = Path.Combine(AppContext.BaseDirectory, "KV.bin");
        if (!File.Exists(kvPath))
            throw new FileNotFoundException(
                "KV.bin not found alongside executable. This file is required for re-signing Xbox 360 containers.",
                kvPath);
        return File.ReadAllBytes(kvPath);
    }

    public static byte[] Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("KV.bin not found at the specified path.", path);
        return File.ReadAllBytes(path);
    }
}
