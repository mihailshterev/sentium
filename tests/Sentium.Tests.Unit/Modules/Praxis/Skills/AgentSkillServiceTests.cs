using NSubstitute;
using Sentium.Core.Results;
using Sentium.Praxis.Entities;
using Sentium.Praxis.Features.Skills;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Skills;

public sealed class AgentSkillServiceTests
{
    private readonly IAgentSkillRepository _repository = Substitute.For<IAgentSkillRepository>();
    private readonly AgentSkillService _sut;

    public AgentSkillServiceTests()
    {
        _sut = new AgentSkillService(_repository, new PassThroughScopedCache());
    }

    private static AgentSkill MakeSkill(Guid? id = null, string name = "Test Skill")
    {
        return new()
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
    }

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
        Assert.Single(result);
        Assert.Equal("Test Skill", result[0].Name);
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
        Assert.NotNull(result);
        Assert.Equal(id, result!.Id);
        Assert.Equal(skill.Name, result.Name);
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
        Assert.Null(result);
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
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("Duplicate", result.Error, StringComparison.Ordinal);
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
        Assert.Equal(ResultStatus.Success, result.Status);
        Assert.NotNull(result.Value);
        Assert.Equal("New Skill", result.Value!.Name);
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
        Assert.False(result);
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
        Assert.True(result);
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
        Assert.True(result);
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
        Assert.False(result);
    }
}
