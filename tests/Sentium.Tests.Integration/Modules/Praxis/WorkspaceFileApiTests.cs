using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sentium.Core.Results;
using Sentium.Praxis.Features.Conversations;
using Sentium.Praxis.Features.Storage;
using Sentium.Praxis.Features.Workspaces;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Praxis;

/// <summary>
/// Workspace files over HTTP: upload rules, extracted-text preview, downloads, re-indexing, the default upload
/// workspace, the workspace delete cascade, and binding a conversation to a workspace. File content lives in an
/// in-memory store behind the factory's substituted <see cref="ILocalFileService"/>.
/// </summary>
public sealed class WorkspaceFileApiTests : IClassFixture<WorkspaceApiFactory>
{
    private readonly HttpClient _client;
    private readonly ConcurrentDictionary<Guid, byte[]> _blobs = new();

    public WorkspaceFileApiTests(WorkspaceApiFactory factory)
    {
        _client = factory.CreateClient();

        var files = factory.Services.GetRequiredService<ILocalFileService>();
        files.UploadToWorkspaceAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(call => Store(call.Arg<Stream>()));
        files.GetFileStreamAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => Open(call.Arg<Guid>()));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UploadRules_IncludeDocuments_AndTheSizeLimit()
    {
        var rules = await _client.GetFromJsonAsync<UploadRulesResponse>("/api/praxis/workspaces/files/rules", Ct);

        Assert.NotNull(rules);
        Assert.All(new[] { ".pdf", ".docx", ".md" }, expected => Assert.Contains(expected, rules.Extensions));
        Assert.Equal(100 * 1024 * 1024, rules.MaxFileSizeBytes);
    }

    [Fact]
    public async Task UploadedPdf_IsPreviewedAsExtractedText()
    {
        var workspace = await CreateWorkspaceAsync();
        var file = await UploadAsync("report.pdf", BuildPdf("Revenue grew nine percent"), workspace.Id);

        var text = await _client.GetFromJsonAsync<WorkspaceFileText>($"/api/praxis/workspaces/files/{file.Id}/text", Ct);

        Assert.Contains("Revenue grew nine percent", text!.Text, StringComparison.Ordinal);
        Assert.False(text.Truncated);
    }

    [Fact]
    public async Task Preview_IsCutAtMaxChars()
    {
        var file = await UploadAsync("long.md", Encoding.UTF8.GetBytes(new string('a', 500)), (await CreateWorkspaceAsync()).Id);

        var text = await _client.GetFromJsonAsync<WorkspaceFileText>($"/api/praxis/workspaces/files/{file.Id}/text?maxChars=100", Ct);

        Assert.Equal(100, text!.Text.Length);
        Assert.True(text.Truncated);
        Assert.Equal(500, text.TotalChars);
    }

    [Fact]
    public async Task Download_IsAnAttachment_AndNeverServesHtmlAsHtml()
    {
        var file = await UploadAsync("page.html", Encoding.UTF8.GetBytes("<script>alert(1)</script>"), (await CreateWorkspaceAsync()).Id);

        var response = await _client.GetAsync($"/api/praxis/workspaces/files/{file.Id}/content", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("<script>alert(1)</script>", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Upload_WithoutWorkspace_GoesToChatUploads()
    {
        var file = await UploadAsync("loose.txt", Encoding.UTF8.GetBytes("loose"), workspaceId: null);

        Assert.NotNull(file.WorkspaceId);
        var workspace = await _client.GetFromJsonAsync<WorkspaceDto>($"/api/praxis/workspaces/{file.WorkspaceId}", Ct);
        Assert.Equal(WorkspaceService.DefaultWorkspaceName, workspace!.Name);
    }

    [Fact]
    public async Task Upload_RejectsAnOverlongFileName()
    {
        using var form = Form(new string('n', 600) + ".md", "x"u8.ToArray(), workspaceId: null);

        var response = await _client.PostAsync("/api/praxis/workspaces/files", form, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // validation happens before anything is stored
        Assert.Empty(_blobs);
    }

    [Fact]
    public async Task Reindex_Accepts_AKnownFile_AndRejectsAnUnknownOne()
    {
        var file = await UploadAsync("notes.md", "# notes"u8.ToArray(), (await CreateWorkspaceAsync()).Id);

        Assert.Equal(HttpStatusCode.Accepted, (await _client.PostAsync($"/api/praxis/workspaces/files/{file.Id}/reindex", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"/api/praxis/workspaces/files/{Guid.NewGuid()}/reindex", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task DeletingAWorkspace_DeletesItsFiles()
    {
        var workspace = await CreateWorkspaceAsync();
        var file = await UploadAsync("doomed.md", "bye"u8.ToArray(), workspace.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/praxis/workspaces/{workspace.Id}", Ct)).StatusCode);

        var all = await _client.GetFromJsonAsync<List<WorkspaceFileDto>>("/api/praxis/workspaces/files", Ct);
        // a deleted workspace must not leave orphaned files behind
        Assert.DoesNotContain(all!, f => f.Id == file.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/praxis/workspaces/files/{file.Id}/text", Ct)).StatusCode);
    }

    [Fact]
    public async Task Conversation_CanBeBoundToAWorkspace_AndUnbound()
    {
        var workspace = await CreateWorkspaceAsync();
        var created = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest(null, "test-model", WorkspaceId: workspace.Id), Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var summary = await created.Content.ReadFromJsonAsync<ConversationSummary>(Ct);
        Assert.Equal(workspace.Name, summary!.WorkspaceName);

        var unbind = await _client.PutAsJsonAsync($"/api/praxis/conversations/{summary.Id}/workspace", new UpdateConversationWorkspaceRequest(null), Ct);
        Assert.Equal(HttpStatusCode.NoContent, unbind.StatusCode);

        var detail = await _client.GetFromJsonAsync<ConversationResponse>($"/api/praxis/conversations/{summary.Id}", Ct);
        Assert.Null(detail!.WorkspaceId);
    }

    [Fact]
    public async Task Conversation_CannotBeBoundToAnUnknownWorkspace()
    {
        var created = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest(null, "test-model"), Ct);
        var summary = await created.Content.ReadFromJsonAsync<ConversationSummary>(Ct);

        var bind = await _client.PutAsJsonAsync($"/api/praxis/conversations/{summary!.Id}/workspace", new UpdateConversationWorkspaceRequest(Guid.NewGuid()), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, bind.StatusCode);
    }

    private Guid Store(Stream content)
    {
        using var copy = new MemoryStream();
        content.CopyTo(copy);
        var blob = Guid.NewGuid();
        _blobs[blob] = copy.ToArray();
        return blob;
    }

    private Stream Open(Guid blob)
    {
        return _blobs.TryGetValue(blob, out var bytes) ? new MemoryStream(bytes) : throw new FileNotFoundException();
    }

    [Fact]
    public async Task WorkspaceFiles_ArePaged_AndSearchableByName()
    {
        var workspace = await CreateWorkspaceAsync();
        for (var i = 0; i < 5; i++)
        {
            await UploadAsync($"note-{i}.md", Encoding.UTF8.GetBytes($"note {i}"), workspace.Id);
        }

        await UploadAsync("Tax-Return-2025.md", Encoding.UTF8.GetBytes("taxes"), workspace.Id);

        var first = await _client.GetFromJsonAsync<PagedResponse<WorkspaceFileDto>>($"/api/praxis/workspaces/{workspace.Id}/files/page?page=1&pageSize=4", Ct);
        var second = await _client.GetFromJsonAsync<PagedResponse<WorkspaceFileDto>>($"/api/praxis/workspaces/{workspace.Id}/files/page?page=2&pageSize=4", Ct);
        Assert.Equal(6, first!.TotalCount);
        Assert.Equal(4, first.Items.Count);
        Assert.Equal(2, second!.Items.Count);
        Assert.Empty(first.Items.Select(f => f.Id).Intersect(second.Items.Select(f => f.Id)));

        var found = await _client.GetFromJsonAsync<PagedResponse<WorkspaceFileDto>>($"/api/praxis/workspaces/{workspace.Id}/files/page?search=tax-return", Ct);
        Assert.Equal("Tax-Return-2025.md", Assert.Single(found!.Items).FileName);

        var anywhere = await _client.GetFromJsonAsync<List<WorkspaceFileDto>>("/api/praxis/workspaces/files?search=TAX-RETURN-2025", Ct);
        Assert.Contains(anywhere!, f => f.FileName == "Tax-Return-2025.md");

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/praxis/workspaces/{Guid.NewGuid()}/files/page", Ct)).StatusCode);
    }

    private async Task<WorkspaceDto> CreateWorkspaceAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/praxis/workspaces", new CreateWorkspaceRequest($"Files-{Guid.NewGuid()}", null), Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkspaceDto>(Ct))!;
    }

    private async Task<WorkspaceFileDto> UploadAsync(string fileName, byte[] content, Guid? workspaceId)
    {
        using var form = Form(fileName, content, workspaceId);
        var response = await _client.PostAsync("/api/praxis/workspaces/files", form, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkspaceFileDto>(Ct))!;
    }

    private static MultipartFormDataContent Form(string fileName, byte[] content, Guid? workspaceId)
    {
        // The form owns and disposes the parts added to it.
        var form = new MultipartFormDataContent();
#pragma warning disable CA2000
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        if (workspaceId is { } id)
        {
            form.Add(new StringContent(id.ToString()), "workspaceId");
        }
#pragma warning restore CA2000

        return form;
    }

    private static byte[] BuildPdf(string line)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4).AddText(line, 12, new PdfPoint(25, 700), font);
        return builder.Build();
    }
}
