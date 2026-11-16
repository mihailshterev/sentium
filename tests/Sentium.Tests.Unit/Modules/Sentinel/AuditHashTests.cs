using FluentAssertions;
using Sentium.Sentinel.Features.Audit;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sentinel;

public sealed class AuditHashTests
{
    [Fact]
    public void Of_ReturnsDeterministicLowerHexSha256()
    {
        const string input = "What is the capital of France?";

        var hash = AuditHash.Of(input);

        hash.Should().Be(AuditHash.Of(input));
        hash.Should().HaveLength(64).And.MatchRegex("^[0-9a-f]+$");
        AuditHash.Of("other").Should().NotBe(hash);
    }
}
