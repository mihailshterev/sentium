using FluentAssertions;
using Sentium.Infrastructure.Storage;
using Xunit;

namespace Sentium.Tests.Unit.Infrastructure.Storage;

public sealed class ObjectUriTests
{
    private const string Bucket = "sandbox-artifacts";

    [Theory]
    [InlineData("job/result.csv")]
    [InlineData("job/out/my chart.png")]
    [InlineData("job/100% done #1?.txt")]
    [InlineData("job/résumé ünïcode.md")]
    public void Create_RoundTripsTheKey(string key)
    {
        var uri = ObjectUri.Create(Bucket, key);

        uri.Scheme.Should().Be(ObjectUri.Scheme);
        ObjectUri.GetKey(uri, Bucket).Should().Be(key);
    }

    [Theory]
    [InlineData("s3://workspace-storage/abc/result.csv")]
    [InlineData("s3://sandbox-artifacts/")]
    [InlineData("http://storage:8333/sandbox-artifacts/abc/result.csv")]
    [InlineData("file:///sandbox-artifacts/abc/result.csv")]
    public void GetKey_ReturnsNull_ForAnythingButAnS3ReferenceIntoTheBucket(string uri)
    {
        ObjectUri.GetKey(new Uri(uri), Bucket).Should().BeNull();
    }
}
