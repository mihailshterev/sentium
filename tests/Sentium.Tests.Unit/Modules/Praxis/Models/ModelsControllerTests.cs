using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Inference;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Features.Models;
using Sentium.Praxis.Features.Registry;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Models;

public sealed class ModelsControllerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IModelCatalog _catalog = Substitute.For<IModelCatalog>();
    private readonly IRegistrySettingsService _settings = Substitute.For<IRegistrySettingsService>();

    public ModelsControllerTests()
    {
        _settings.GetAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(SettingsSnapshot.Default);
    }

    private ModelsController Controller(bool sovereign = true)
    {
        var identity = new ClaimsIdentity(
            sovereign ? [new Claim(RoleClaims.ShortRoleClaimType, SecurityRoles.Sovereign)] : [],
            authenticationType: "test");

        return new ModelsController(_catalog, _settings, Options.Create(new InferenceOptions { DefaultModel = "config-default" }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    [Fact]
    public async Task GetModels_ReturnsTheCatalogAndTheRegistryDefaultModel()
    {
        _catalog.ListAsync(Arg.Any<CancellationToken>()).Returns([new CatalogModel("gemma-4-e4b", "loaded", 16384)]);
        _settings.GetAsync(null, Arg.Any<CancellationToken>())
            .Returns(SettingsSnapshot.Default with { Inference = new InferenceSettingsSnapshot("gemma-4-e4b", 0.3f) });

        var result = await Controller().GetModels(Ct);

        var body = result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ModelsResponse>().Which;
        body.DefaultModel.Should().Be("gemma-4-e4b");
        body.Models.Should().Equal(new ModelInfo("gemma-4-e4b", "loaded", 16384));
    }

    [Fact]
    public async Task GetModels_FallsBackToTheConfiguredDefault_WhenRegistrySettingsAreMissing()
    {
        _catalog.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        _settings.GetAsync(null, Arg.Any<CancellationToken>()).Returns(SettingsSnapshot.Default with { Inference = null });

        var result = await Controller().GetModels(Ct);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<ModelsResponse>()
            .Which.DefaultModel.Should().Be("config-default");
    }

    [Fact]
    public async Task GetModels_Returns503_WhenTheInferenceServerIsUnavailable()
    {
        _catalog.ListAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<CatalogModel>?)null);

        var result = await Controller().GetModels(Ct);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Theory]
    [InlineData(nameof(ModelActionResult.Accepted), StatusCodes.Status202Accepted)]
    [InlineData(nameof(ModelActionResult.NotFound), StatusCodes.Status404NotFound)]
    [InlineData(nameof(ModelActionResult.Conflict), StatusCodes.Status409Conflict)]
    [InlineData(nameof(ModelActionResult.Failed), StatusCodes.Status503ServiceUnavailable)]
    public async Task LoadModel_MapsTheCatalogResult(string outcome, int expectedStatus)
    {
        _catalog.LoadAsync("gemma-4-e4b", Arg.Any<CancellationToken>()).Returns(Enum.Parse<ModelActionResult>(outcome));

        var result = await Controller().LoadModel(new ModelActionRequest("gemma-4-e4b"), Ct);

        result.Should().BeAssignableTo<IStatusCodeActionResult>().Which.StatusCode.Should().Be(expectedStatus);
    }

    [Fact]
    public async Task UnloadModel_CallsTheCatalog()
    {
        _catalog.UnloadAsync("gemma-4-e4b", Arg.Any<CancellationToken>()).Returns(ModelActionResult.Accepted);

        var result = await Controller().UnloadModel(new ModelActionRequest("gemma-4-e4b"), Ct);

        result.Should().BeOfType<AcceptedResult>();
        await _catalog.Received(1).UnloadAsync("gemma-4-e4b", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAndUnload_AreForbidden_ForNonSovereignUsers()
    {
        var controller = Controller(sovereign: false);

        (await controller.LoadModel(new ModelActionRequest("gemma-4-e4b"), Ct)).Should().BeOfType<ForbidResult>();
        (await controller.UnloadModel(new ModelActionRequest("gemma-4-e4b"), Ct)).Should().BeOfType<ForbidResult>();

        await _catalog.DidNotReceiveWithAnyArgs().LoadAsync(default!, Ct);
        await _catalog.DidNotReceiveWithAnyArgs().UnloadAsync(default!, Ct);
    }
}
