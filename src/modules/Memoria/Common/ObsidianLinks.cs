namespace Sentium.Memoria.Common;

internal static class ObsidianLinks
{
    /// <summary>
    /// An <c>obsidian://open</c> URI that opens the note in the Obsidian app on the reader's machine.
    /// </summary>
    public static string Open(string vaultName, string path)
    {
        ArgumentNullException.ThrowIfNull(vaultName);
        ArgumentNullException.ThrowIfNull(path);

        var file = path.EndsWith(VaultPaths.NoteExtension, StringComparison.OrdinalIgnoreCase)
            ? path[..^VaultPaths.NoteExtension.Length]
            : path;

        return $"obsidian://open?vault={Uri.EscapeDataString(vaultName)}&file={Uri.EscapeDataString(file)}";
    }
}
