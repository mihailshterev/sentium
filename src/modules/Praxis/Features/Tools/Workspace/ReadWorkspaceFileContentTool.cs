using System.ComponentModel;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Data;
using Sentium.Praxis.Features.Files;
using Sentium.Praxis.Features.Storage;

namespace Sentium.Praxis.Features.Tools.Workspace;

/// <summary>
/// An agent tool that reads one of the acting user's workspace files as text (PDF and DOCX extracted), a window of
/// lines at a time so a large file never floods the context.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Medium, Resource = ToolResource.File, Action = ToolAction.Read)]
internal sealed class ReadWorkspaceFileContentTool(PraxisDbContext dbContext, IWorkspaceFileTextReader textReader, IPdpContextAccessor runContext) : IAgentTool
{
    internal const int MaxCharsPerRead = 24_000;

    private const int DefaultMaxLines = 400;

    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            ReadAsync,
            ToolNames.ReadWorkspaceFileContent,
            $"Reads the text of a workspace file (PDF and Word documents included), identified by its GUID. Returns up to {DefaultMaxLines} lines " +
            $"or {MaxCharsPerRead} characters per call; when more remains, call again with the startLine it suggests.");
    }

    public async Task<string> ReadAsync(
        [Description("The GUID of the file to read (from list_workspace_files or the attached-files context).")] string fileId,
        [Description("1-based line to start reading at (default 1).")] int? startLine = null,
        [Description("Maximum number of lines to return (default 400).")] int? maxLines = null,
        CancellationToken ct = default)
    {
        if (runContext.UserId is not { } userId)
        {
            return WorkspaceToolScope.NoUserError;
        }

        if (!Guid.TryParse(fileId?.Trim().Trim('"', '{', '}'), out var id))
        {
            return "Error: A valid file GUID is required.";
        }

        try
        {
            var projectFile = await dbContext.ProjectFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, ct);

            if (projectFile is null)
            {
                return $"Error: File with ID {id} not found.";
            }

            var text = await textReader.ReadAsync(projectFile.BlobName, projectFile.Extension, ct);
            var slice = TextWindow.Slice(text, startLine ?? 1, maxLines ?? DefaultMaxLines, MaxCharsPerRead);

            return Format(projectFile.FileName, slice);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"Error: Failed to read file content: {ex.Message}";
        }
    }

    private static string Format(string fileName, TextSlice slice)
    {
        if (slice.TotalLines <= 1 && slice.Text.Length == 0)
        {
            return $"[{fileName}: the file has no text content.]";
        }

        var header = new StringBuilder()
            .Append('[').Append(fileName).Append(": lines ").Append(slice.StartLine).Append('-').Append(slice.EndLine)
            .Append(" of ").Append(slice.TotalLines);

        header.Append(slice.HasMore ? $". More remains: call again with startLine={slice.EndLine + 1}.]" : ".]");

        return $"{header}\n{slice.Text}";
    }
}
