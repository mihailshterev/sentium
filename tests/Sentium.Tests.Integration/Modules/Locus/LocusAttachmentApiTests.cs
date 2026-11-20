using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Sentium.Locus.Entities;
using Sentium.Locus.Features.Assets;
using Sentium.Locus.Features.Attachments;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Locus;

public sealed class LocusAttachmentApiTests(LocusApiFactory factory) : IClassFixture<LocusApiFactory>
{
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<AssetDto> CreateAssetAsync()
    {
        var home = await _client.CreateLocationAsync(LocusApi.Unique("Home"), ct: Ct);
        return await _client.CreateAssetAsync(new SaveAssetRequest("Camera", home.Id, IsAgentAccessible: false), Ct);
    }

    private async Task<HttpResponseMessage> UploadAsync(Guid assetId, byte[] content, string fileName, string claimedType, AttachmentKind kind = AttachmentKind.Photo)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(content);
        using var kindField = new StringContent(kind.ToString());
        file.Headers.ContentType = new MediaTypeHeaderValue(claimedType);
        form.Add(file, "file", fileName);
        form.Add(kindField, "kind");
        return await _client.PostAsync($"/api/locus/assets/{assetId}/attachments", form, Ct);
    }

    [Fact]
    public async Task UploadedImage_RoundTripsWithSafeHeaders()
    {
        var asset = await CreateAssetAsync();

        var upload = await UploadAsync(asset.Id, PngBytes, "front ☺.png", "application/octet-stream");

        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var attachment = await upload.Content.ReadFromJsonAsync<AttachmentDto>(Ct);
        // the type comes from the bytes, not the client
        Assert.Equal("image/png", attachment!.ContentType);
        Assert.True(attachment.IsImage);
        Assert.Equal("front ☺.png", attachment.FileName);
        Assert.Equal(AttachmentKind.Photo, attachment.Kind);

        var content = await _client.GetAsync(attachment.ContentUrl, Ct);
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal("image/png", content.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", Assert.Single(content.Headers.GetValues("X-Content-Type-Options")));
        Assert.Contains("sandbox", content.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.Equal(PngBytes, await content.Content.ReadAsByteArrayAsync(Ct));

        var listed = await _client.GetFromJsonAsync<List<AttachmentDto>>($"/api/locus/assets/{asset.Id}/attachments", Ct);
        Assert.Equal(attachment.Id, Assert.Single(listed!).Id);

        var summary = await _client.GetFromJsonAsync<AssetDto>($"/api/locus/assets/{asset.Id}", Ct);
        Assert.Single(summary!.Attachments);
    }

    [Fact]
    public async Task PdfIsAlwaysServedAsADownload()
    {
        var asset = await CreateAssetAsync();
        var upload = await UploadAsync(asset.Id, Encoding.ASCII.GetBytes("%PDF-1.7\n%test"), "manual.pdf", "application/pdf", AttachmentKind.Manual);
        var attachment = await upload.Content.ReadFromJsonAsync<AttachmentDto>(Ct);

        var content = await _client.GetAsync(attachment!.ContentUrl, Ct);

        Assert.Equal("attachment", content.Content.Headers.ContentDisposition!.DispositionType);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>", "logo.svg", "image/svg+xml")]
    [InlineData("MZ\u0090\u0000binary", "photo.png", "image/png")]
    public async Task UnsupportedOrDisguisedFiles_AreRejected(string content, string fileName, string claimedType)
    {
        var asset = await CreateAssetAsync();

        var upload = await UploadAsync(asset.Id, Encoding.UTF8.GetBytes(content), fileName, claimedType);

        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
    }

    [Fact]
    public async Task UploadToUnknownAsset_Returns404()
    {
        var upload = await UploadAsync(Guid.NewGuid(), PngBytes, "x.png", "image/png");

        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);
    }

    [Fact]
    public async Task DeletingAnAttachment_RemovesRowAndFile()
    {
        var asset = await CreateAssetAsync();
        var attachment = await (await UploadAsync(asset.Id, PngBytes, "x.png", "image/png")).Content.ReadFromJsonAsync<AttachmentDto>(Ct);

        var delete = await _client.DeleteAsync($"/api/locus/assets/{asset.Id}/attachments/{attachment!.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.DoesNotContain($"{asset.Id}/{attachment.Id}", factory.AttachmentStore.Blobs.Keys);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(attachment.ContentUrl, Ct)).StatusCode);
    }

    [Fact]
    public async Task DeletingAnAsset_PurgesItsFiles()
    {
        var asset = await CreateAssetAsync();
        await UploadAsync(asset.Id, PngBytes, "a.png", "image/png");
        await UploadAsync(asset.Id, PngBytes, "b.png", "image/png");
        Assert.Equal(2, factory.AttachmentStore.Blobs.Keys.Count(k => k.StartsWith($"{asset.Id}/", StringComparison.Ordinal)));

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/locus/assets/{asset.Id}", Ct)).StatusCode);

        Assert.DoesNotContain(factory.AttachmentStore.Blobs.Keys, k => k.StartsWith($"{asset.Id}/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssetSummaries_ExposeTheFirstImageAsCover()
    {
        var asset = await CreateAssetAsync();
        await UploadAsync(asset.Id, Encoding.ASCII.GetBytes("%PDF-1.7\n"), "manual.pdf", "application/pdf", AttachmentKind.Manual);
        var image = await (await UploadAsync(asset.Id, PngBytes, "front.png", "image/png")).Content.ReadFromJsonAsync<AttachmentDto>(Ct);

        var page = await _client.GetFromJsonAsync<Sentium.Core.Results.PagedResponse<AssetSummaryDto>>($"/api/locus/assets?locationId={asset.LocationId}", Ct);

        var summary = Assert.Single(page!.Items);
        Assert.Equal(2, summary.AttachmentCount);
        Assert.Equal(image!.ContentUrl, summary.CoverImageUrl);
    }
}
