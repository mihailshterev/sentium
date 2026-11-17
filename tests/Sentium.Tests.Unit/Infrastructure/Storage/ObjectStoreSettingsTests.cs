using FluentAssertions;
using Sentium.Infrastructure.Storage;
using Xunit;

namespace Sentium.Tests.Unit.Infrastructure.Storage;

public sealed class ObjectStoreSettingsTests
{
    [Fact]
    public void Parse_ReadsEndpointAndCredentials_WithTheDefaultRegion()
    {
        var settings = ObjectStoreSettings.Parse("Endpoint=http://storage:8333;AccessKey=ak;SecretKey=s3cr3t+/=");

        settings.Endpoint.Should().Be(new Uri("http://storage:8333"));
        settings.AccessKey.Should().Be("ak");
        settings.SecretKey.Should().Be("s3cr3t+/=");
        settings.Region.Should().Be(ObjectStoreSettings.DefaultRegion);
    }

    [Fact]
    public void Parse_HonoursAnExplicitRegion_AndIgnoresKeyCase()
    {
        var settings = ObjectStoreSettings.Parse("endpoint=https://s3.lan;accesskey=ak;secretkey=sk;region=eu-west-1");

        settings.Region.Should().Be("eu-west-1");
        settings.Endpoint.Scheme.Should().Be("https");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("AccessKey=ak;SecretKey=sk")]
    [InlineData("Endpoint=storage:8333;AccessKey=ak;SecretKey=sk")]
    [InlineData("Endpoint=ftp://storage;AccessKey=ak;SecretKey=sk")]
    [InlineData("Endpoint=http://storage:8333;SecretKey=sk")]
    [InlineData("Endpoint=http://storage:8333;AccessKey=ak;SecretKey=")]
    [InlineData("UseDevelopmentStorage=true")]
    public void Parse_RejectsIncompleteConnectionStrings(string? connectionString)
    {
        var parse = () => ObjectStoreSettings.Parse(connectionString);

        parse.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ErrorsAndToString_NeverContainTheSecret()
    {
        const string secret = "very-secret-value";

        var settings = ObjectStoreSettings.Parse($"Endpoint=http://storage:8333;AccessKey=ak;SecretKey={secret}");
        var invalid = () => ObjectStoreSettings.Parse($"Endpoint=nope;AccessKey=ak;SecretKey={secret}");

        settings.ToString().Should().NotContain(secret);
        invalid.Should().Throw<InvalidOperationException>().Which.Message.Should().NotContain(secret);
    }
}
