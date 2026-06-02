using FluentAssertions;
using NSubstitute;
using Sentium.AgentRuntime.Core.Entities;
using Sentium.AgentRuntime.Core.Skills;
using Sentium.AgentRuntime.Infrastructure.Skills;
using Sentium.Shared.Results;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class AgentSkillServiceTests
{
    private readonly IAgentSkillRepository _repository = Substitute.For<IAgentSkillRepository>();
    private readonly AgentSkillService _sut;

    public AgentSkillServiceTests()
    {
        _sut = new AgentSkillService(_repository, new PassThroughScopedCache());
    }

    private static AgentSkill MakeSkill(Guid? id = null, string name = "Test Skill") =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Description = "A test skill",
            Instructions = "Do something",
            SkillType = AgentSkillType.Custom,
            FileName = null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    [Fact]
    public async Task GetAllAsync_ReturnsAllSkills_WhenCalled()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var skills = new List<AgentSkill> { MakeSkill() };
        _repository.GetAllAsync(ct).Returns(skills);

        // Act
        var result = await _sut.GetAllAsync(ct);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Test Skill");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDto_WhenFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var skill = MakeSkill(id);
        _repository.GetByIdAsync(id, ct).Returns(skill);

        // Act
        var result = await _sut.GetByIdAsync(id, ct);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.Name.Should().Be(skill.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, ct).Returns((AgentSkill?)null);

        // Act
        var result = await _sut.GetByIdAsync(id, ct);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ReturnsConflict_WhenNameExists()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateAgentSkillRequest("Duplicate", "Desc", "Instructions", AgentSkillType.Custom, null);
        _repository.GetByNameAsync(request.Name, ct).Returns(MakeSkill(name: "Duplicate"));

        // Act
        var result = await _sut.CreateAsync(request, ct);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        result.Error.Should().Contain("Duplicate");
        await _repository.DidNotReceive().AddAsync(Arg.Any<AgentSkill>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ReturnsSuccess_WhenNameUnique()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateAgentSkillRequest("New Skill", "Desc", "Instructions", AgentSkillType.Custom, null);
        _repository.GetByNameAsync(request.Name, ct).Returns((AgentSkill?)null);

        // Act
        var result = await _sut.CreateAsync(request, ct);

        // Assert
        result.Status.Should().Be(ResultStatus.Success);
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("New Skill");
        await _repository.Received(1).AddAsync(Arg.Any<AgentSkill>(), ct);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsFalse_WhenSkillNotFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, ct).Returns((AgentSkill?)null);

        // Act
        var result = await _sut.UpdateAsync(id, new UpdateAgentSkillRequest("Updated", "Desc", "Inst"), ct);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_ReturnsTrue_WhenFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var skill = MakeSkill(id);
        _repository.GetByIdAsync(id, ct).Returns(skill);

        // Act
        var result = await _sut.UpdateAsync(id, new UpdateAgentSkillRequest("Updated", "Desc", "Inst"), ct);

        // Assert
        result.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(skill, ct);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsTrue_WhenDeleted()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _repository.DeleteAsync(id, ct).Returns(true);

        // Act
        var result = await _sut.DeleteAsync(id, ct);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _repository.DeleteAsync(id, ct).Returns(false);

        // Act
        var result = await _sut.DeleteAsync(id, ct);

        // Assert
        result.Should().BeFalse();
    }
}
