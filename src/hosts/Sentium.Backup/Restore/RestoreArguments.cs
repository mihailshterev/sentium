namespace Sentium.Backup.Restore;

internal sealed record RestoreArguments(string ArchivePath, string? IdentityPath, bool Replace, bool VerifyOnly)
{
    public const string RestoreVerb = "restore";
    public const string VerifyVerb = "verify";

    public const string Usage = """
        Usage:
          restore <archive> [--identity <age identity file>] [--replace]
          verify  <archive> [--identity <age identity file>]
        """;

    public static bool IsCommand(string[] args) => args.Length > 0 && args[0] is RestoreVerb or VerifyVerb;

    public static RestoreArguments? Parse(string[] args)
    {
        if (!IsCommand(args) || args.Length < 2)
        {
            return null;
        }

        string? identity = null;
        var replace = false;
        for (var i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--identity" when i + 1 < args.Length:
                    identity = args[++i];
                    break;
                case "--replace":
                    replace = true;
                    break;
                default:
                    return null;
            }
        }

        var verifyOnly = args[0] == VerifyVerb;
        return verifyOnly && replace ? null : new RestoreArguments(args[1], identity, replace, verifyOnly);
    }
}
