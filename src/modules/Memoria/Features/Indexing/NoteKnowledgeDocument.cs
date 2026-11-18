using System.Security.Cryptography;
using System.Text;
using Sentium.Codex.Contracts.Models;
using Sentium.Memoria.Common;
using Sentium.Memoria.Entities;
using Sentium.Memoria.Features.Parsing;

namespace Sentium.Memoria.Features.Indexing;

/// <summary>
/// What a note becomes in the <see cref="KnowledgeCollections.VaultNotes"/> collection: heading-aware chunks tagged
/// with the note id, path and tags, private to the note's owner.
/// </summary>
internal static class NoteKnowledgeDocument
{
    public const string NoteIdKey = "vault_note_id";
    public const string PathKey = "path";
    public const string TagsKey = "tags";
    public const string FolderKey = "folder";

    /// <summary>
    /// Bump when the chunking or metadata changes so startup reconciliation re-indexes every note.
    /// </summary>
    private const string FormatVersion = "1";

    public static string SourceFor(Guid noteId) => $"memoria:note:{noteId}";

    /// <summary>
    /// Fingerprint of everything the knowledge-base copy is built from.
    /// </summary>
    public static string ComputeHash(string path, string contentHash)
    {
        var input = $"{FormatVersion}\n{path}\n{contentHash}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }

    public static IngestionRequest Build(VaultNote note)
    {
        ArgumentNullException.ThrowIfNull(note);

        var parsed = MarkdownNoteParser.Parse(note.Path, note.Content);
        var chunks = MarkdownNoteChunker.Chunk(note.Title, note.Tags, parsed.Sections);
        var folder = VaultPaths.FolderOf(note.Path);

        return new IngestionRequest
        {
            Content = note.Content,
            Chunks = chunks,
            Source = SourceFor(note.Id),
            SourceType = IngestionSourceType.Obsidian,
            Scope = KnowledgeScope.User,
            UserId = note.UserId,
            Metadata = new Dictionary<string, string>
            {
                [NoteIdKey] = note.Id.ToString(),
                [PathKey] = note.Path,
                [FolderKey] = folder,
                [TagsKey] = string.Join(',', note.Tags)
            }
        };
    }
}
