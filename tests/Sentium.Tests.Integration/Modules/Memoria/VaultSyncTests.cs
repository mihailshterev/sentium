using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Core.Results;
using Sentium.Infrastructure.Security;
using Sentium.Memoria.Data;
using Sentium.Memoria.Features.Notes;
using Sentium.Memoria.Features.Sync;
using Sentium.Tests.Integration.Common;
using Sentium.Tests.Integration.Modules.Identity;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Memoria;

/// <summary>
/// Satelles syncs a vault with its Device token; the owner reads it in the portal. Background workers do not run here,
/// so embedding is only checked as queued outbox rows.
/// </summary>
public sealed class VaultSyncTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private const string DeviceVault = "/api/memoria/device/vault";
    private const string PortalVault = "/api/memoria/vault";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sync_RoundTrip_ResolvesLinksAndBacklinks_AndQueuesIndexing()
    {
        var (owner, device) = await PairAsync("DESKTOP-VAULT");
        using var portal = factory.CreateClientFor(owner);

        (await device.PutAsJsonAsync(DeviceVault, new ClaimVaultRequest("Second Brain"), Ct)).EnsureSuccessStatusCode();

        var hub = Note("Hub.md", "---\ntags: [moc]\n---\n# Hub\nSee [[Ideas]] and [[Projects/Sentium|Sentium]] and [[Missing]].");
        var ideas = Note("Ideas.md", "Ideas link back to [[Hub]]. #thinking");
        var sentium = Note("Projects/Sentium.md", "The project. ![[Ideas]]");

        var manifest = await PostAsync<VaultManifestResponse>(device, $"{DeviceVault}/manifest",
            new VaultManifestRequest([new(hub.Path, hub.Hash), new(ideas.Path, ideas.Hash), new(sentium.Path, sentium.Hash)]));
        manifest.Upload.Should().BeEquivalentTo(hub.Path, ideas.Path, sentium.Path);

        var uploaded = await PutAsync<VaultSyncResult>(device, $"{DeviceVault}/notes", new UploadNotesRequest([hub, ideas, sentium]));
        uploaded.Changed.Should().Be(3);
        uploaded.NoteCount.Should().Be(3);

        var again = await PostAsync<VaultManifestResponse>(device, $"{DeviceVault}/manifest",
            new VaultManifestRequest([new(hub.Path, hub.Hash), new(ideas.Path, ideas.Hash), new(sentium.Path, sentium.Hash)]));
        again.Upload.Should().BeEmpty();

        var summary = (await portal.GetFromJsonAsync<VaultSummaryDto>(PortalVault, Ct))!;
        summary.Name.Should().Be("Second Brain");
        summary.DeviceName.Should().Be("DESKTOP-VAULT");
        summary.NoteCount.Should().Be(3);
        summary.UnresolvedLinkCount.Should().Be(1);

        var ideasNote = await GetNoteByPathAsync(portal, "Ideas.md");
        ideasNote.Tags.Should().Equal("thinking");
        ideasNote.Backlinks.Select(b => b.Path).Should().BeEquivalentTo("Hub.md", "Projects/Sentium.md");
        ideasNote.ObsidianUri.Should().Be("obsidian://open?vault=Second%20Brain&file=Ideas");

        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        var db = scope.ServiceProvider.GetRequiredService<MemoriaDbContext>();
        (await db.IndexEntries.CountAsync(e => e.UserId == owner, Ct)).Should().Be(3);
    }

    [Fact]
    public async Task Move_KeepsTheNoteAndItsBacklinks_AndRemoveLeavesLinksUnresolved()
    {
        var (owner, device) = await PairAsync("DESKTOP-MOVE");
        using var portal = factory.CreateClientFor(owner);
        (await device.PutAsJsonAsync(DeviceVault, new ClaimVaultRequest("Vault"), Ct)).EnsureSuccessStatusCode();

        await PutAsync<VaultSyncResult>(device, $"{DeviceVault}/notes", new UploadNotesRequest(
        [
            Note("Inbox/Target.md", "Target note."),
            Note("Source.md", "Points at [[Target]].")
        ]));

        var before = await GetNoteByPathAsync(portal, "Inbox/Target.md");

        await PostAsync<VaultSyncResult>(device, $"{DeviceVault}/notes/move", new MoveNoteRequest("Inbox/Target.md", "Archive/Target.md"));

        var moved = await GetNoteByPathAsync(portal, "Archive/Target.md");
        moved.Id.Should().Be(before.Id);
        moved.Backlinks.Select(b => b.Path).Should().Equal("Source.md");

        await PostAsync<VaultSyncResult>(device, $"{DeviceVault}/notes/remove", new RemoveNotesRequest(["Archive/Target.md"]));

        var source = await GetNoteByPathAsync(portal, "Source.md");
        source.Links.Should().ContainSingle().Which.Target.Should().BeNull();
    }

    [Fact]
    public async Task Manifest_RemovesNotesTheVaultNoLongerHas()
    {
        var (owner, device) = await PairAsync("DESKTOP-RECONCILE");
        using var portal = factory.CreateClientFor(owner);
        (await device.PutAsJsonAsync(DeviceVault, new ClaimVaultRequest("Vault"), Ct)).EnsureSuccessStatusCode();

        var kept = Note("Kept.md", "Still here.");
        await PutAsync<VaultSyncResult>(device, $"{DeviceVault}/notes", new UploadNotesRequest([kept, Note("Gone.md", "Deleted offline.")]));

        var result = await PostAsync<VaultManifestResponse>(device, $"{DeviceVault}/manifest", new VaultManifestRequest([new(kept.Path, kept.Hash)]));

        result.Removed.Should().Be(1);
        result.NoteCount.Should().Be(1);
        var notes = (await portal.GetFromJsonAsync<PagedResponse<VaultNoteSummaryDto>>($"{PortalVault}/notes", Ct))!;
        notes.Items.Select(n => n.Path).Should().Equal("Kept.md");
    }

    [Fact]
    public async Task AnotherDevice_NeedsATakeover_AndThenTheFirstDeviceIsLockedOut()
    {
        var owner = await factory.CreateUserAsync();
        using var first = factory.CreateDeviceClient((await factory.PairDirectlyAsync(owner, "FIRST", Ct)).Token);
        using var second = factory.CreateDeviceClient((await factory.PairDirectlyAsync(owner, "SECOND", Ct)).Token);

        (await first.PutAsJsonAsync(DeviceVault, new ClaimVaultRequest("Vault"), Ct)).EnsureSuccessStatusCode();

        (await second.PutAsJsonAsync(DeviceVault, new ClaimVaultRequest("Vault"), Ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await second.PutAsJsonAsync(DeviceVault, new ClaimVaultRequest("Vault", Takeover: true), Ct)).EnsureSuccessStatusCode();

        var response = await first.PutAsJsonAsync($"{DeviceVault}/notes", new UploadNotesRequest([Note("A.md", "a")]), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Upload_RejectsAHashThatDoesNotMatchTheContent_AndUnsafePaths()
    {
        var (_, device) = await PairAsync("DESKTOP-HASH");
        (await device.PutAsJsonAsync(DeviceVault, new ClaimVaultRequest("Vault"), Ct)).EnsureSuccessStatusCode();

        var forged = Note("A.md", "real") with { Content = "tampered" };
        (await device.PutAsJsonAsync($"{DeviceVault}/notes", new UploadNotesRequest([forged]), Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var escaping = Note("../outside.md", "x");
        (await device.PutAsJsonAsync($"{DeviceVault}/notes", new UploadNotesRequest([escaping]), Ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TheVault_IsPrivate_EvenFromASovereign()
    {
        var (owner, device) = await PairAsync("DESKTOP-PRIVATE");
        (await device.PutAsJsonAsync(DeviceVault, new ClaimVaultRequest("Private"), Ct)).EnsureSuccessStatusCode();
        await PutAsync<VaultSyncResult>(device, $"{DeviceVault}/notes", new UploadNotesRequest([Note("Diary.md", "Private thoughts.")]));

        var someoneElse = await factory.CreateUserAsync();
        using var member = factory.CreateClientFor(someoneElse);
        using var sovereign = factory.CreateClient();

        (await member.GetAsync(PortalVault, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sovereign.GetAsync(PortalVault, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sovereign.GetFromJsonAsync<PagedResponse<VaultNoteSummaryDto>>($"{PortalVault}/notes", Ct))!.Items.Should().BeEmpty();

        using var ownerPortal = factory.CreateClientFor(owner);
        (await ownerPortal.DeleteAsync(PortalVault, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ownerPortal.GetAsync(PortalVault, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<(Guid Owner, HttpClient Device)> PairAsync(string deviceName)
    {
        var owner = await factory.CreateUserAsync();
        var paired = await factory.PairDirectlyAsync(owner, deviceName, Ct);
        return (owner, factory.CreateDeviceClient(paired.Token));
    }

    private static async Task<VaultNoteDto> GetNoteByPathAsync(HttpClient portal, string path)
    {
        var page = (await portal.GetFromJsonAsync<PagedResponse<VaultNoteSummaryDto>>($"{PortalVault}/notes?pageSize=100", Ct))!;
        var summary = page.Items.Should().ContainSingle(n => n.Path == path).Subject;
        return (await portal.GetFromJsonAsync<VaultNoteDto>($"{PortalVault}/notes/{summary.Id}", Ct))!;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private static async Task<T> PutAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PutAsJsonAsync(url, body, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private static NoteUpload Note(string path, string content)
    {
        return new NoteUpload(path, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))), content);
    }
}
