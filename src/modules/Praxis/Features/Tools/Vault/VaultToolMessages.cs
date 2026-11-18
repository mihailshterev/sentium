namespace Sentium.Praxis.Features.Tools.Vault;

internal static class VaultToolMessages
{
    public const string NoUser = "Error: The vault belongs to a signed-in user, and no user is associated with this run.";
    public const string Unavailable = "Error: The vault could not be read right now.";
    public const string NoVault = "The user has not synced an Obsidian vault. They can sync one from the Sentium Satelles desktop app.";
    public const string NoMatches = "No notes in the user's Obsidian vault match this search (or no vault has been synced yet).";
    public const string NoNoteGiven = "Error: Name the note by its title, path or alias.";

    public const int DefaultTopK = 8;

    public static string NoteNotFound(string note)
        => $"No note in the user's vault matches \"{VaultToolFormatter.OneLine(note, 120)}\". Use {ToolNames.SearchVault} to find it.";
}
