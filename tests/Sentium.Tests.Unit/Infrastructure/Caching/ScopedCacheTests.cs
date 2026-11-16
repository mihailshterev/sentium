using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentium.Infrastructure.Caching;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;
using Xunit;

namespace Sentium.Tests.Unit.Infrastructure.Caching;

/// <summary>
/// Values are cached per caller scope, but a write from any scope must evict every scope's copy.
/// </summary>
public sealed class ScopedCacheTests : IDisposable
{
    private const string Tag = "widgets";
    private const string Key = "widgets:all";

    private static readonly Guid Owner = Guid.NewGuid();

    private readonly ServiceProvider _services;
    private int _loads;

    public ScopedCacheTests()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddHybridCache();
        builder.AddSentiumPlatform();
        _services = builder.Services.BuildServiceProvider();
    }

    private static Type CacheType => typeof(IScopedCache).Assembly.GetType("Sentium.Infrastructure.Caching.ScopedCache", throwOnError: true)!;

    public void Dispose() => _services.Dispose();

    [Fact]
    public async Task CachesPerScope()
    {
        await ReadAsync(User(Owner));
        await ReadAsync(User(Owner));
        await ReadAsync(Sovereign());

        _loads.Should().Be(2);
    }

    [Fact]
    public async Task InvalidateTag_FromAnotherScope_EvictsTheOwnersCopy()
    {
        await ReadAsync(User(Owner));

        await CacheFor(Sovereign()).InvalidateTagAsync(Tag, TestContext.Current.CancellationToken);
        await ReadAsync(User(Owner));

        _loads.Should().Be(2);
    }

    [Fact]
    public async Task Remove_FromBackgroundScope_EvictsTheOwnersCopy()
    {
        await ReadAsync(User(Owner));

        await CacheFor(Background()).RemoveAsync(Key, TestContext.Current.CancellationToken);
        await ReadAsync(User(Owner));

        _loads.Should().Be(2);
    }

    private async Task ReadAsync(ICurrentUser user)
    {
        await CacheFor(user).GetOrCreateAsync(Key, LoadAsync, Tag, TestContext.Current.CancellationToken);
    }

    private ValueTask<int> LoadAsync(CancellationToken ct)
    {
        return ValueTask.FromResult(Interlocked.Increment(ref _loads));
    }

    private IScopedCache CacheFor(ICurrentUser user)
    {
        return ActivatorUtilities.CreateInstance(_services, CacheType, user) as IScopedCache
            ?? throw new InvalidOperationException("ScopedCache could not be created.");
    }

    private static ICurrentUser User(Guid id) => new StubUser(id, IsSovereign: false, IsSystem: false);

    private static ICurrentUser Sovereign() => new StubUser(Guid.NewGuid(), IsSovereign: true, IsSystem: false);

    private static ICurrentUser Background() => new StubUser(null, IsSovereign: false, IsSystem: true);

    private sealed record StubUser(Guid? UserId, bool IsSovereign, bool IsSystem) : ICurrentUser
    {
        public bool IsAuthenticated => UserId is not null;
    }
}
