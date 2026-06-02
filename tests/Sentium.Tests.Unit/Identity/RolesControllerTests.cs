using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.Identity.Api.Contracts.Roles;
using Sentium.Identity.Api.Controllers;
using Sentium.Identity.Application.Abstractions;
using System.Security.Claims;
using Xunit;

namespace Sentium.Tests.Unit.Identity;

public sealed class RolesControllerTests
{
    private readonly IRoleService _roleService = Substitute.For<IRoleService>();
    private readonly RolesController _controller;

    public RolesControllerTests()
    {
        _controller = new RolesController(_roleService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Test"))
                }
            }
        };
    }

    private void SetRequesterId(Guid id)
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, id.ToString())], "Test"))
            }
        };
    }

    [Fact]
    public void GetRoles_ReturnsOk_WithRoleList()
    {
        // Act
        var result = _controller.GetRoles();

        // Assert
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUserRoles_ReturnsOk_WithRolesList()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        _roleService.GetRolesAsync(userId, ct).Returns(new List<string> { "Admin" });

        // Act
        var result = await _controller.GetUserRoles(userId, ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.As<IList<string>>().Should().ContainSingle("Admin");
    }

    [Fact]
    public async Task AssignRole_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var requesterId = Guid.NewGuid();
        SetRequesterId(requesterId);
        var request = new AssignRoleRequest(Guid.NewGuid(), "Admin");
        _roleService.AssignRoleAsync(requesterId, request.UserId, request.RoleName, ct)
            .Returns((true, (string?)null));

        // Act
        var result = await _controller.AssignRole(request, ct);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task AssignRole_ReturnsBadRequest_WhenHierarchyViolated()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var requesterId = Guid.NewGuid();
        SetRequesterId(requesterId);
        var request = new AssignRoleRequest(Guid.NewGuid(), "Sovereign");
        _roleService.AssignRoleAsync(requesterId, request.UserId, request.RoleName, ct)
            .Returns((false, "Only Sovereign users can assign the Sovereign role."));

        // Act
        var result = await _controller.AssignRole(request, ct);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RemoveRole_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var requesterId = Guid.NewGuid();
        SetRequesterId(requesterId);
        var request = new RemoveRoleRequest(Guid.NewGuid(), "Admin");
        _roleService.RemoveRoleAsync(requesterId, request.UserId, request.RoleName, ct)
            .Returns((true, (string?)null));

        // Act
        var result = await _controller.RemoveRole(request, ct);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RemoveRole_ReturnsBadRequest_WhenLastSovereign()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var requesterId = Guid.NewGuid();
        SetRequesterId(requesterId);
        var request = new RemoveRoleRequest(Guid.NewGuid(), "Sovereign");
        _roleService.RemoveRoleAsync(requesterId, request.UserId, request.RoleName, ct)
            .Returns((false, "Cannot remove the last Sovereign."));

        // Act
        var result = await _controller.RemoveRole(request, ct);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
