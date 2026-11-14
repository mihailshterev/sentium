using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
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

        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var attachment = await upload.Content.ReadFromJsonAsync<AttachmentDto>(Ct);
        attachment!.ContentType.Should().Be("image/png", "the type comes from the bytes, not the client");
        attachment.IsImage.Should().BeTrue();
        attachment.FileName.Should().Be("front ☺.png");
        attachment.Kind.Should().Be(AttachmentKind.Photo);

        var content = await _client.GetAsync(attachment.ContentUrl, Ct);
        content.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        content.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        content.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("sandbox");
        (await content.Content.ReadAsByteArrayAsync(Ct)).Should().Equal(PngBytes);

        var listed = await _client.GetFromJsonAsync<List<AttachmentDto>>($"/api/locus/assets/{asset.Id}/attachments", Ct);
        listed.Should().ContainSingle().Which.Id.Should().Be(attachment.Id);

        var summary = await _client.GetFromJsonAsync<AssetDto>($"/api/locus/assets/{asset.Id}", Ct);
        summary!.Attachments.Should().ContainSingle();
    }

    [Fact]
    public async Task PdfIsAlwaysServedAsADownload()
    {
        var asset = await CreateAssetAsync();
        var upload = await UploadAsync(asset.Id, Encoding.ASCII.GetBytes("%PDF-1.7\n%test"), "manual.pdf", "application/pdf", AttachmentKind.Manual);
        var attachment = await upload.Content.ReadFromJsonAsync<AttachmentDto>(Ct);

        var content = await _client.GetAsync(attachment!.ContentUrl, Ct);

        content.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>", "logo.svg", "image/svg+xml")]
    [InlineData("MZ\u0090\u0000binary", "photo.png", "image/png")]
    public async Task UnsupportedOrDisguisedFiles_AreRejected(string content, string fileName, string claimedType)
    {
        var asset = await CreateAssetAsync();

        var upload = await UploadAsync(asset.Id, Encoding.UTF8.GetBytes(content), fileName, claimedType);

        upload.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadToUnknownAsset_Returns404()
    {
        var upload = await UploadAsync(Guid.NewGuid(), PngBytes, "x.png", "image/png");

        upload.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletingAnAttachment_RemovesRowAndFile()
    {
        var asset = await CreateAssetAsync();
        var attachment = await (await UploadAsync(asset.Id, PngBytes, "x.png", "image/png")).Content.ReadFromJsonAsync<AttachmentDto>(Ct);

        var delete = await _client.DeleteAsync($"/api/locus/assets/{asset.Id}/attachments/{attachment!.Id}", Ct);

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        factory.AttachmentStore.Blobs.Keys.Should().NotContain($"{asset.Id}/{attachment.Id}");
        (await _client.GetAsync(attachment.ContentUrl, Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletingAnAsset_PurgesItsFiles()
    {
        var asset = await CreateAssetAsync();
        await UploadAsync(asset.Id, PngBytes, "a.png", "image/png");
        await UploadAsync(asset.Id, PngBytes, "b.png", "image/png");
        factory.AttachmentStore.Blobs.Keys.Count(k => k.StartsWith($"{asset.Id}/", StringComparison.Ordinal)).Should().Be(2);

        (await _client.DeleteAsync($"/api/locus/assets/{asset.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        factory.AttachmentStore.Blobs.Keys.Should().NotContain(k => k.StartsWith($"{asset.Id}/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AssetSummaries_ExposeTheFirstImageAsCover()
    {
        var asset = await CreateAssetAsync();
        await UploadAsync(asset.Id, Encoding.ASCII.GetBytes("%PDF-1.7\n"), "manual.pdf", "application/pdf", AttachmentKind.Manual);
        var image = await (await UploadAsync(asset.Id, PngBytes, "front.png", "image/png")).Content.ReadFromJsonAsync<AttachmentDto>(Ct);

        var page = await _client.GetFromJsonAsync<Sentium.Core.Results.PagedResponse<AssetSummaryDto>>($"/api/locus/assets?locationId={asset.LocationId}", Ct);

        var summary = page!.Items.Should().ContainSingle().Subject;
        summary.AttachmentCount.Should().Be(2);
        summary.CoverImageUrl.Should().Be(image!.ContentUrl);
    }
}
