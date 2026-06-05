using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.Registry.Api.Controllers;
using Sentium.Registry.Application.Settings;
using Sentium.Registry.Core.Settings;
using Sentium.Shared.Constants;
using Xunit;

namespace Sentium.Tests.Unit.Registry;

public sealed class SettingsControllerTests
{
    private readonly ISettingsService _service = Substitute.For<ISettingsService>();

    private readonly ISettingsCatalog _catalog = new SettingsCatalog(new ISettingsDescriptor[]
    {
        new SettingsDescriptor<HarnessSettings>(SettingsKeys.Harness, SettingsScope.PerUser, c => c.Harness, (c, v) => c.Harness = v),
        new SettingsDescriptor<PdpSettings>(SettingsKeys.Pdp, SettingsScope.Global, c => c.Pdp, (c, v) => c.Pdp = v),
    });

    private SettingsController NewController(params Claim[] claims) =>
        new(_service, _catalog)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
            }
        };

    private static SettingsEnvelope Env(string key, object value) => new(key, value, DateTimeOffset.UtcNow, null);
    private static JsonElement Payload(object value) => JsonSerializer.SerializeToElement(value);

    [Fact]
    public async Task Get_UnknownKey_ReturnsNotFound()
    {
        var controller = NewController(new Claim(ClaimTypes.Name, "member"));
        var result = await controller.Get("nope", null, TestContext.Current.CancellationToken);
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Get_Harness_ReturnsOk_ForUser()
    {
        var ct = TestContext.Current.CancellationToken;
        _service.GetAsync(SettingsKeys.Harness, Arg.Any<Guid?>(), ct).Returns(Env(SettingsKeys.Harness, new HarnessSettings()));
        var controller = NewController(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

        var result = await controller.Get(SettingsKeys.Harness, null, ct);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Get_Harness_SystemCaller_HonorsUserIdQuery()
    {
        var ct = TestContext.Current.CancellationToken;
        var target = Guid.NewGuid();
        _service.GetAsync(SettingsKeys.Harness, Arg.Any<Guid?>(), ct).Returns(Env(SettingsKeys.Harness, new HarnessSettings()));
        var controller = NewController(new Claim("caller-type", "internal-system"));

        await controller.Get(SettingsKeys.Harness, target, ct);

        await _service.Received(1).GetAsync(SettingsKeys.Harness, target, ct);
    }

    [Fact]
    public async Task Get_Pdp_Forbids_NonSovereign()
    {
        var controller = NewController(new Claim(ClaimTypes.Name, "member"));
        var result = await controller.Get(SettingsKeys.Pdp, null, TestContext.Current.CancellationToken);
        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Get_Pdp_ReturnsOk_ForSovereign()
    {
        var ct = TestContext.Current.CancellationToken;
        _service.GetAsync(SettingsKeys.Pdp, null, ct).Returns(Env(SettingsKeys.Pdp, new PdpSettings()));
        var controller = NewController(new Claim(ClaimTypes.Role, "Sovereign"));

        var result = await controller.Get(SettingsKeys.Pdp, null, ct);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Update_Pdp_Forbids_NonSovereign()
    {
        var ct = TestContext.Current.CancellationToken;
        var controller = NewController(new Claim(ClaimTypes.Name, "member"));
        var result = await controller.Update(SettingsKeys.Pdp, Payload(new { lockdownMode = true }), ct);
        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Update_Pdp_ReturnsOk_ForSovereign()
    {
        var ct = TestContext.Current.CancellationToken;
        _service.UpdateAsync(SettingsKeys.Pdp, null, Arg.Any<JsonElement>(), Arg.Any<string?>(), ct)
            .Returns(Env(SettingsKeys.Pdp, new PdpSettings()));
        var controller = NewController(new Claim(ClaimTypes.Role, "Sovereign"), new Claim(ClaimTypes.Name, "admin"));

        var result = await controller.Update(SettingsKeys.Pdp, Payload(new { lockdownMode = true }), ct);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Update_Harness_PassesCallerUserId_ToService()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        _service.UpdateAsync(SettingsKeys.Harness, userId, Arg.Any<JsonElement>(), Arg.Any<string?>(), ct)
            .Returns(Env(SettingsKeys.Harness, new HarnessSettings()));
        var controller = NewController(new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Name, "member"));

        await controller.Update(SettingsKeys.Harness, Payload(new { userHarnessPrompt = "x" }), ct);

        await _service.Received(1).UpdateAsync(SettingsKeys.Harness, userId, Arg.Any<JsonElement>(), "member", ct);
    }
}
