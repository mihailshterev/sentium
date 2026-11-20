using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Identity.Features.Account;
using Sentium.Identity.Features.Avatars;
using Sentium.Identity.Features.Users;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Identity;

public sealed class AvatarApiTests(AvatarApiFactory factory) : IClassFixture<AvatarApiFactory>
{
    private const string AvatarPath = "/api/identity/account/me/avatar";

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UploadedPicture_IsServedWithSafeHeadersAndShownOnTheProfile()
    {
        var userId = await factory.CreateUserAsync();
        using var client = ClientFor(userId);

        var upload = await UploadAsync(client, PngBytes, "application/octet-stream");

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var avatarUrl = (await upload.Content.ReadFromJsonAsync<AvatarResponse>(Ct))!.AvatarUrl;
        Assert.StartsWith($"/api/identity/users/{userId}/avatar?v=", avatarUrl, StringComparison.Ordinal);

        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/identity/account/me", Ct);
        Assert.Equal(avatarUrl, profile!.AvatarUrl);

        // Another signed-in user may see the picture.
        using var bystander = ClientFor(await factory.CreateUserAsync());
        var content = await bystander.GetAsync(avatarUrl, Ct);
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal("image/png", content.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", Assert.Single(content.Headers.GetValues("X-Content-Type-Options")));
        Assert.Contains("sandbox", content.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.True(content.Headers.CacheControl!.Private);
        Assert.Equal(PngBytes, await content.Content.ReadAsByteArrayAsync(Ct));
    }

    [Fact]
    public async Task ScriptableOrUnknownFiles_AreRejected()
    {
        using var client = ClientFor(await factory.CreateUserAsync());
        var svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

        var upload = await UploadAsync(client, svg, "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
    }

    [Fact]
    public async Task RemovedPicture_IsNoLongerServed()
    {
        var userId = await factory.CreateUserAsync();
        using var client = ClientFor(userId);
        var upload = await UploadAsync(client, PngBytes, "image/png");
        var avatarUrl = (await upload.Content.ReadFromJsonAsync<AvatarResponse>(Ct))!.AvatarUrl;

        var removed = await client.DeleteAsync(AvatarPath, Ct);

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/identity/account/me", Ct);
        Assert.Null(profile!.AvatarUrl);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(avatarUrl, Ct)).StatusCode);
    }

    [Fact]
    public async Task DeletingTheUser_DeletesTheirPicture()
    {
        var userId = await factory.CreateUserAsync();
        using var client = ClientFor(userId);
        await UploadAsync(client, PngBytes, "image/png");
        Assert.True(factory.ObjectStore.Contains(AvatarService.Bucket, AvatarService.ObjectKey(userId)));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var (succeeded, errors) = await scope.ServiceProvider.GetRequiredService<IUserService>().DeleteUserAsync(Guid.NewGuid(), userId, Ct);
            Assert.True(succeeded, string.Join(", ", errors));
        }

        Assert.False(factory.ObjectStore.Contains(AvatarService.Bucket, AvatarService.ObjectKey(userId)));
    }

    private HttpClient ClientFor(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());
        return client;
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] content, string claimedType)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(claimedType);
        form.Add(file, "file", "me.png");
        return await client.PostAsync(AvatarPath, form, Ct);
    }
}
