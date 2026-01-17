using IdentityProvider.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace IdentityProvider.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    IAuthService authService,
    ILogger<AuthController> logger) : ControllerBase
{
    private readonly IAuthService _authService = authService;
    private readonly ILogger<AuthController> _logger = logger;
}

