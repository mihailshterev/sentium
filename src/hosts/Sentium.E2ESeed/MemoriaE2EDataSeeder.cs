using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Infrastructure.Testing;
using Sentium.Memoria.Common;
using Sentium.Memoria.Data;
using Sentium.Memoria.Entities;
using Sentium.Memoria.Features.Links;
using Sentium.Memoria.Features.Parsing;

namespace Sentium.E2ESeed;

/// <summary>
/// A small synced vault for the Playwright suite (Testing environment only): three linked notes and one link to a note
/// that does not exist, as Satelles would have uploaded them.
/// </summary>
internal sealed class MemoriaE2EDataSeeder : IE2ESeeder
{
    public const string VaultName = "e2e-vault";
    public const string HubTitle = "e2e-hub";

    private static readonly (string Path, string Content)[] Notes =
    [
        ("e2e-hub.md", "---\ntags: [moc]\n---\n# Hub\nStart at [[e2e-ideas]] and [[Projects/e2e-project|the project]]. Later: [[e2e-missing]]."),
        ("e2e-ideas.md", "Ideas link back to [[e2e-hub]]. #thinking"),
        ("Projects/e2e-project.md", "The project, built from [[e2e-ideas]].")
    ];

    public Task SeedAsync(IServiceProvider services, E2ESeedContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(context);

        return SeedAsync(services.GetRequiredService<MemoriaDbContext>(), context.UserId, context.TimeProvider, ct);
    }

    private static async Task SeedAsync(MemoriaDbContext db, Guid userId, TimeProvider timeProvider, CancellationToken ct)
    {
        if (await db.Vaults.AnyAsync(v => v.UserId == userId, ct))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var vault = new Vault { Id = Guid.CreateVersion7(), UserId = userId, Name = VaultName, CreatedAt = now, LastSyncAt = now };
        db.Vaults.Add(vault);

        var parsed = Notes
            .Select(n => (Note: NewNote(vault, n.Path, n.Content, now), Parsed: MarkdownNoteParser.Parse(n.Path, n.Content)))
            .ToList();

        foreach (var (note, result) in parsed)
        {
            note.Tags = [.. result.Tags];
            db.Notes.Add(note);
        }

        var resolver = new LinkResolver(parsed.Select(p => new NoteKey(p.Note.Id, p.Note.PathKey, [])));
        foreach (var (note, result) in parsed)
        {
            db.Links.AddRange(result.Links.Select(link => new NoteLink
            {
                Id = Guid.CreateVersion7(),
                SourceNoteId = note.Id,
                UserId = userId,
                Target = link.Target,
                TargetKey = link.TargetKey,
                Heading = link.Heading,
                IsEmbed = link.IsEmbed,
                TargetNoteId = resolver.Resolve(link.TargetKey)
            }));
        }

        await db.SaveChangesAsync(ct);
    }

    private static VaultNote NewNote(Vault vault, string path, string content, DateTimeOffset now)
    {
        return new VaultNote
        {
            Id = Guid.CreateVersion7(),
            VaultId = vault.Id,
            UserId = vault.UserId,
            Path = path,
            PathKey = VaultPaths.ToKey(path),
            Title = VaultPaths.TitleOf(path),
            ContentHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))),
            Content = content,
            UpdatedAt = now
        };
    }
}
