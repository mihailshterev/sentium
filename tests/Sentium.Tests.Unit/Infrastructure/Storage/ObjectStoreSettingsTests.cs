using Sentium.Infrastructure.Storage;
using Xunit;

namespace Sentium.Tests.Unit.Infrastructure.Storage;

public sealed class ObjectStoreSettingsTests
{
    [Fact]
    public void Parse_ReadsEndpointAndCredentials_WithTheDefaultRegion()
    {
        var settings = ObjectStoreSettings.Parse("Endpoint=http://storage:8333;AccessKey=ak;SecretKey=s3cr3t+/=");

        Assert.Equal(new Uri("http://storage:8333"), settings.Endpoint);
        Assert.Equal("ak", settings.AccessKey);
        Assert.Equal("s3cr3t+/=", settings.SecretKey);
        Assert.Equal(ObjectStoreSettings.DefaultRegion, settings.Region);
    }

    [Fact]
    public void Parse_HonoursAnExplicitRegion_AndIgnoresKeyCase()
    {
        var settings = ObjectStoreSettings.Parse("endpoint=https://s3.lan;accesskey=ak;secretkey=sk;region=eu-west-1");

        Assert.Equal("eu-west-1", settings.Region);
        Assert.Equal("https", settings.Endpoint.Scheme);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("AccessKey=ak;SecretKey=sk")]
    [InlineData("Endpoint=storage:8333;AccessKey=ak;SecretKey=sk")]
    [InlineData("Endpoint=ftp://storage;AccessKey=ak;SecretKey=sk")]
    [InlineData("Endpoint=http://storage:8333;SecretKey=sk")]
    [InlineData("Endpoint=http://storage:8333;AccessKey=ak;SecretKey=")]
    public void Parse_RejectsIncompleteConnectionStrings(string? connectionString)
    {
        var parse = () => ObjectStoreSettings.Parse(connectionString);

        Assert.ThrowsAny<InvalidOperationException>(parse);
    }

    [Fact]
    public void ErrorsAndToString_NeverContainTheSecret()
    {
        const string secret = "very-secret-value";

        var settings = ObjectStoreSettings.Parse($"Endpoint=http://storage:8333;AccessKey=ak;SecretKey={secret}");
        var invalid = () => ObjectStoreSettings.Parse($"Endpoint=nope;AccessKey=ak;SecretKey={secret}");

        Assert.DoesNotContain(secret, settings.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, Assert.ThrowsAny<InvalidOperationException>(invalid).Message, StringComparison.Ordinal);
    }
}
