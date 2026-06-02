using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.AgentRuntime.Api.Controllers;
using Sentium.AgentRuntime.Core.Entities;
using Sentium.AgentRuntime.Core.Skills;
using Sentium.Shared.Results;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class SkillsControllerTests
{
    private readonly IAgentSkillService _skillService = Substitute.For<IAgentSkillService>();
    private readonly IBuiltInSkillCatalog _builtInCatalog = Substitute.For<IBuiltInSkillCatalog>();
    private readonly SkillsController _controller;

    public SkillsControllerTests()
    {
        _controller = new SkillsController(_skillService, _builtInCatalog);
    }

    private static AgentSkillDto MakeDto(Guid? id = null, string name = "Test Skill") =>
        new(id ?? Guid.NewGuid(), name, "A test skill", "Instructions", AgentSkillType.Custom, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    [Fact]
    public void GetBuiltInSkills_ReturnsOk_WithCatalogList()
    {
        // Arrange
        var skills = new List<BuiltInSkillInfo> { new("Skill1", "Description", "Instructions") };
        _builtInCatalog.GetAll().Returns(skills);

        // Act
        var result = _controller.GetBuiltInSkills();

        // Assert
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(skills);
    }

    [Fact]
    public async Task GetSkills_ReturnsOk_WithAllSkills()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var skills = new List<AgentSkillDto> { MakeDto() };
        _skillService.GetAllAsync(ct).Returns(skills);

        // Act
        var result = await _controller.GetSkills(ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(skills);
    }

    [Fact]
    public async Task GetSkillById_ReturnsOk_WhenFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var dto = MakeDto(id);
        _skillService.GetByIdAsync(id, ct).Returns(dto);

        // Act
        var result = await _controller.GetSkillById(id, ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(dto);
    }

    [Fact]
    public async Task GetSkillById_ReturnsNotFound_WhenNull()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _skillService.GetByIdAsync(id, ct).Returns((AgentSkillDto?)null);

        // Act
        var result = await _controller.GetSkillById(id, ct);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateSkill_ReturnsCreated_OnSuccess()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateAgentSkillRequest("New Skill", "Desc", "Instructions", AgentSkillType.Custom);
        var dto = MakeDto(name: "New Skill");
        _skillService.CreateAsync(request, ct).Returns(Result<AgentSkillDto>.Success(dto));

        // Act
        var result = await _controller.CreateSkill(request, ct);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().Be(dto);
    }

    [Fact]
    public async Task CreateSkill_ReturnsConflict_WhenNameExists()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateAgentSkillRequest("Duplicate", "Desc", "Instructions", AgentSkillType.Custom);
        _skillService.CreateAsync(request, ct)
            .Returns(Result<AgentSkillDto>.Conflict("A skill named 'Duplicate' already exists."));

        // Act
        var result = await _controller.CreateSkill(request, ct);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task UpdateSkill_ReturnsNoContent_WhenUpdated()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _skillService.UpdateAsync(id, Arg.Any<UpdateAgentSkillRequest>(), ct).Returns(true);

        // Act
        var result = await _controller.UpdateSkill(id, new UpdateAgentSkillRequest("Updated", "Desc", "Inst"), ct);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateSkill_ReturnsNotFound_WhenMissing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _skillService.UpdateAsync(id, Arg.Any<UpdateAgentSkillRequest>(), ct).Returns(false);

        // Act
        var result = await _controller.UpdateSkill(id, new UpdateAgentSkillRequest("Updated", "Desc", "Inst"), ct);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteSkill_ReturnsNoContent_WhenDeleted()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _skillService.DeleteAsync(id, ct).Returns(true);

        // Act
        var result = await _controller.DeleteSkill(id, ct);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteSkill_ReturnsNotFound_WhenMissing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _skillService.DeleteAsync(id, ct).Returns(false);

        // Act
        var result = await _controller.DeleteSkill(id, ct);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UploadSkill_ReturnsBadRequest_WhenFileIsNull()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await _controller.UploadSkill(null!, ct);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadSkill_ReturnsBadRequest_WhenNotMarkdown()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var file = Substitute.For<IFormFile>();
        file.FileName.Returns("skill.txt");
        file.Length.Returns(100L);

        // Act
        var result = await _controller.UploadSkill(file, ct);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadSkill_ReturnsCreated_WhenValidMarkdownFile()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var content = "# My Skill\n\nDo something useful.";
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);

        var file = Substitute.For<IFormFile>();
        file.FileName.Returns("my-skill.md");
        file.Length.Returns((long)bytes.Length);
        file.OpenReadStream().Returns(new MemoryStream(bytes));

        var dto = MakeDto(name: "my-skill");
        _skillService.CreateAsync(Arg.Any<CreateAgentSkillRequest>(), ct)
            .Returns(Result<AgentSkillDto>.Success(dto));

        // Act
        var result = await _controller.UploadSkill(file, ct);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
    }
}
