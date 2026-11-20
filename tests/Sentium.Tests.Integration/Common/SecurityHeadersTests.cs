using Xunit;

namespace Sentium.Tests.Integration.Common;

public sealed class SecurityHeadersTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    [Fact]
    public async Task ApiResponses_CarryTheSecurityHeaders()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/identity/account/registration", TestContext.Current.CancellationToken);

        Assert.Equal(new string[] { "nosniff" }, response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Equal(new string[] { "no-referrer" }, response.Headers.GetValues("Referrer-Policy"));
        var actual = response.Headers.GetValues("Permissions-Policy").Single();
        Assert.Contains("camera=()", actual, StringComparison.Ordinal);
        Assert.Contains("microphone=(self)", actual, StringComparison.Ordinal);
    }
}
