namespace Sentium.Praxis.Features.Tools.Inventory;

internal static class InventoryToolText
{
    /// <summary>
    /// Small models send blank strings for arguments they mean to omit; treat those as absent.
    /// </summary>
    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
