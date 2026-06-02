using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.Registry.Api.Controllers;
using Sentium.Registry.Core.Settings;
using System.Security.Claims;
using Xunit;

namespace Sentium.Tests.Unit.Registry;

public sealed class SettingsControllerTests
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly SettingsController _controller;

    public SettingsControllerTests()
    {
        _controller = new SettingsController(_settingsService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Name, "admin-user")], "Test"))
                }
            }
        };
    }

    private static SettingsDto MakeDto() =>
        new(new HarnessSettingsDto("", true, true), DateTimeOffset.UtcNow, null);

    [Fact]
    public async Task GetSettings_ReturnsOk_WithSettingsDto()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _settingsService.GetAsync(ct).Returns(MakeDto());

        // Act
        var result = await _controller.GetSettings(ct);

        // Assert — controller returns ActionResult<SettingsDto>; unwrap .Result
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateSettings_ReturnsOk_WithUpdatedDto()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var updated = new SettingsDto(new HarnessSettingsDto("new prompt", true, false), DateTimeOffset.UtcNow, "admin-user");
        _settingsService.GetAsync(ct).Returns(updated);
        var request = new UpdateSettingsRequest(new UpdateHarnessSettingsRequest("new prompt", true, false));

        // Act
        var result = await _controller.UpdateSettings(request, ct);

        // Assert — controller returns ActionResult<SettingsDto>; unwrap .Result
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateSettings_PassesCallerIdentity_ToService()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _settingsService.GetAsync(ct).Returns(MakeDto());
        var request = new UpdateSettingsRequest(new UpdateHarnessSettingsRequest("", true, true));

        // Act
        await _controller.UpdateSettings(request, ct);

        // Assert
        await _settingsService.Received(1).UpdateAsync(request, "admin-user", ct);
    }

    [Fact]
    public async Task UpdateSettings_CallsGetAsync_AfterUpdate()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _settingsService.GetAsync(ct).Returns(MakeDto());
        var request = new UpdateSettingsRequest(new UpdateHarnessSettingsRequest("", true, true));

        // Act
        await _controller.UpdateSettings(request, ct);

        // Assert
        await _settingsService.Received(1).GetAsync(ct);
    }
}
