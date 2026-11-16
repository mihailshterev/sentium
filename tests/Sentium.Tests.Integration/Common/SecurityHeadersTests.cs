using FluentAssertions;
using Xunit;

namespace Sentium.Tests.Integration.Common;

public sealed class SecurityHeadersTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    [Fact]
    public async Task ApiResponses_CarryTheSecurityHeaders()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/identity/account/registration", TestContext.Current.CancellationToken);

        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.GetValues("Referrer-Policy").Should().Equal("no-referrer");
        response.Headers.GetValues("Permissions-Policy").Single().Should().Contain("camera=()").And.Contain("microphone=(self)");
    }
}
