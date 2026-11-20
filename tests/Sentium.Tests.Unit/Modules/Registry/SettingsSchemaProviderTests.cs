using System.Text.Json;
using Sentium.Core.Constants;
using Sentium.Registry.Contracts.Settings;
using Sentium.Registry.Features.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Registry;

public sealed class SettingsSchemaProviderTests
{
    private static readonly SettingsCatalog Catalog = SettingsCatalog.FromAssembly(typeof(SettingsCatalog).Assembly);

    private readonly SettingsSchemaProvider _sut = new(Catalog);

    [Theory]
    [InlineData(SettingsKeys.Harness, typeof(HarnessSettings), SettingsScope.Personal)]
    [InlineData(SettingsKeys.UserCalendar, typeof(UserCalendarSettings), SettingsScope.Personal)]
    [InlineData(SettingsKeys.Companion, typeof(CompanionSettings), SettingsScope.Personal)]
    [InlineData(SettingsKeys.Evaluation, typeof(EvaluationSettings), SettingsScope.Personal)]
    [InlineData(SettingsKeys.Inference, typeof(InferenceSettings), SettingsScope.Household)]
    [InlineData(SettingsKeys.Calendar, typeof(CalendarSettings), SettingsScope.Household)]
    [InlineData(SettingsKeys.Pdp, typeof(PdpSettings), SettingsScope.Household)]
    [InlineData(SettingsKeys.Access, typeof(AccessSettings), SettingsScope.Household)]
    [InlineData(SettingsKeys.Mcp, typeof(McpSettings), SettingsScope.Household)]
    [InlineData(SettingsKeys.Vigil, typeof(VigilSettings), SettingsScope.Household)]
    public void Catalog_DiscoversEverySettingsGroup(string key, Type contract, SettingsScope scope)
    {
        Assert.True(Catalog.TryGet(key, out var descriptor));
        Assert.Equal(contract, descriptor.ValueType);
        Assert.Equal(scope, descriptor.Scope);
    }

    [Fact]
    public void Catalog_HasNoOtherGroups()
    {
        Assert.Equal(10, Catalog.All.Count);
    }

    [Fact]
    public void Sections_ListPersonalBeforeHousehold_InOrder()
    {
        Assert.Equal(
            ["agents", "calendar", "companion", "evaluations", "inference", "household-calendar", "security", "access", "mcp", "monitoring"],
            _sut.Sections.Select(s => s.Id));
        Assert.All(_sut.Sections.Take(4), s => Assert.Equal(SettingsSchemaProvider.PersonalScope, s.Scope));
        Assert.All(_sut.Sections.Skip(4), s => Assert.Equal(SettingsSchemaProvider.HouseholdScope, s.Scope));
    }

    [Fact]
    public void SectionIds_AndSettingIds_AreUnique()
    {
        Assert.Equal(_sut.Sections.Count, _sut.Sections.Select(s => s.Id).Distinct().Count());

        var ids = _sut.Sections.SelectMany(s => s.Settings).Select(s => s.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void PropertiesWithoutSettingAttribute_AreNotListed()
    {
        var security = _sut.Sections.Single(s => s.Key == SettingsKeys.Pdp);

        Assert.DoesNotContain(security.Settings, s => s.Field == "lockdownMode");
        Assert.Equal("semanticIntentCheckEnabled", security.Settings[0].Field);
    }

    [Fact]
    public void Defaults_ComeFromTheContractInitializers()
    {
        var setting = Find("inference.defaultModel");

        Assert.Equal(AIModels.Gemma4_E4B, setting.DefaultValue.GetString());
        Assert.Equal(15, Find("vigil.pollIntervalSeconds").DefaultValue.GetInt32());
        Assert.Equal(JsonValueKind.Null, Find("companion.voice").DefaultValue.ValueKind);
        Assert.Equal("08:00:00", Find("calendar.dayStart").DefaultValue.GetString());
    }

    [Fact]
    public void Bounds_ComeFromDataAnnotations()
    {
        var poll = Find("vigil.pollIntervalSeconds").Control;
        Assert.Equal(("number", 5d, 3600d, "s"), (poll.Kind, poll.Min, poll.Max, poll.Unit));

        var rateLimit = Find("pdp.rateLimitMaxRequests").Control;
        Assert.Equal(1, rateLimit.Min);
        Assert.Null(rateLimit.Max);

        Assert.Equal(HarnessSettings.MaxUserHarnessPromptLength, Find("harness.userHarnessPrompt").Control.MaxLength);
    }

    [Fact]
    public void ControlKinds_AreInferredFromPropertyTypes()
    {
        Assert.Equal("switch", Find("mcp.enabled").Control.Kind);
        Assert.Equal("range", Find("inference.agentTemperature").Control.Kind);
        Assert.Equal(0.05, Find("inference.agentTemperature").Control.Step);
        Assert.Equal("time", Find("calendar.dayStart").Control.Kind);
        Assert.Equal("range", Find("pdp.autonomyLevel").Control.Kind);
    }

    [Fact]
    public void Nullability_DrivesNullableAndRequired()
    {
        var userTimeZone = Find("calendar-user.timeZone");
        Assert.True(userTimeZone.Nullable);
        Assert.Equal("timezone", userTimeZone.Control.Kind);

        Assert.False(Find("calendar.timeZone").Nullable);
        Assert.False(Find("calendar-user.dayStart").Control.Required);
        Assert.True(Find("calendar.dayStart").Control.Required);
    }

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public async Task DataAnnotations_RejectOutOfBoundsValues(object settings)
    {
        var valid = await (Task<bool>)typeof(SettingsValidation)
            .GetMethod(nameof(SettingsValidation.IsValidAsync))!
            .MakeGenericMethod(settings.GetType())
            .Invoke(null, [settings, null])!;

        Assert.False(valid);
    }

    public static TheoryData<object> InvalidSettings() =>
    [
        new HarnessSettings { UserHarnessPrompt = new string('x', HarnessSettings.MaxUserHarnessPromptLength + 1) },
        new InferenceSettings { DefaultModel = "" },
        new InferenceSettings { AgentTemperature = 1.5f },
        new PdpSettings { AutonomyLevel = 11 },
        new PdpSettings { RateLimitWindowSeconds = 0 },
        new McpSettings { MaxResultChars = 999 },
        new EvaluationSettings { JudgeModel = new string('m', EvaluationSettings.MaxModelIdLength + 1) },
    ];

    [Fact]
    public async Task Defaults_PassValidation()
    {
        Assert.True(await SettingsValidation.IsValidAsync(new HarnessSettings()));
        Assert.True(await SettingsValidation.IsValidAsync(new InferenceSettings()));
        Assert.True(await SettingsValidation.IsValidAsync(new PdpSettings()));
        Assert.True(await SettingsValidation.IsValidAsync(new McpSettings()));
    }

    private SettingSchema Find(string id) => _sut.Sections.SelectMany(s => s.Settings).Single(s => s.Id == id);
}
