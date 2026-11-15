using System.Formats.Tar;
using Microsoft.Extensions.Options;
using Sentium.Backup.Options;
using Sentium.Backup.Tools;

namespace Sentium.Backup.Parts;

/// <summary>
/// The Sentium.Web data-protection key ring. Without it the restored installation cannot read its own sign-in
/// cookies (everyone signs in again) or the MCP server secrets encrypted at rest (they must be re-entered).
/// </summary>
internal sealed class KeysPart(IOptions<BackupOptions> options)
{
    private string KeysPath => options.Value.DataProtectionKeysPath;

    public async Task<int> ExportAsync(string tarPath, CancellationToken ct)
    {
        if (!Directory.Exists(KeysPath))
        {
            throw new BackupFailedException($"The data-protection key directory {KeysPath} is not mounted.");
        }

        var files = Directory.GetFiles(KeysPath);

        await using var output = File.Create(tarPath);
        await using var writer = new TarWriter(output, TarEntryFormat.Pax, leaveOpen: false);
        foreach (var file in files)
        {
            await writer.WriteEntryAsync(file, Path.GetFileName(file), ct);
        }

        return files.Length;
    }

    public async Task<int> ImportAsync(string tarPath, CancellationToken ct)
    {
        Directory.CreateDirectory(KeysPath);

        var restored = 0;
        await using (var input = File.OpenRead(tarPath))
        await using (var reader = new TarReader(input))
        {
            while (await reader.GetNextEntryAsync(copyData: false, ct) is { } entry)
            {
                if (entry.EntryType != TarEntryType.RegularFile)
                {
                    continue;
                }

                // Key files are flat; a name with a directory part is not from a Sentium backup.
                var name = Path.GetFileName(entry.Name);
                if (name.Length == 0 || name != entry.Name)
                {
                    throw new BackupFailedException($"Unexpected entry '{entry.Name}' in the key part.");
                }

                await entry.ExtractToFileAsync(Path.Combine(KeysPath, name), overwrite: true, ct);
                restored++;
            }
        }

        await ProcessRunner.RunAsync("chown", ["-R", options.Value.KeysOwner, KeysPath], ct);
        return restored;
    }

    public bool HasAnyKey() => Directory.Exists(KeysPath) && Directory.EnumerateFileSystemEntries(KeysPath).Any();

    public void DeleteAll()
    {
        if (!Directory.Exists(KeysPath))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(KeysPath))
        {
            File.Delete(file);
        }
    }
}
