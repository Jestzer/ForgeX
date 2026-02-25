namespace ForgeX.Core.Halo3;

/// <summary>
/// Interface for palette databases that map forge quota indices to display names.
/// </summary>
public interface IPaletteDatabase
{
    string? GetQuotaName(int quotaIndex);
    string? GetVariantName(int quotaIndex, int variantIndex);
    string? GetCategory(int quotaIndex);
}
