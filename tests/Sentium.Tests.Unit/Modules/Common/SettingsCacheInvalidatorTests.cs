using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sentium.Infrastructure.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Modules;

public sealed class SettingsCacheInvalidatorTests
{
    [Fact]
    public async Task InvalidateAsync_CallsEveryHandler_EvenWhenOneThrows()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var failing = Substitute.For<ISettingsCacheInvalidationHandler>();
        failing.TryInvalidateAsync("harness", userId, ct).ThrowsAsync(new InvalidOperationException("cache down"));
        var healthy = Substitute.For<ISettingsCacheInvalidationHandler>();
        var sut = new SettingsCacheInvalidator([failing, healthy], NullLogger<SettingsCacheInvalidator>.Instance);

        var act = () => sut.InvalidateAsync("harness", userId, ct);

        await act.Should().NotThrowAsync();
        await healthy.Received(1).TryInvalidateAsync("harness", userId, ct);
    }
}
