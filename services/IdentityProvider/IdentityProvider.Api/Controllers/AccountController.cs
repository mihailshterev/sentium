using IdentityProvider.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IdentityProvider.Api.Controllers;

[ApiController]
[Route("account")]
public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : ControllerBase
{
    [HttpPost("/register")]
    public async Task<IActionResult> Register(string email, string password)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return Ok();
    }

    [HttpPost("/login")]
    public async Task<IActionResult> Login(string email, string password)
    {
        var result = await signInManager.PasswordSignInAsync(email, password, false, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            return Unauthorized();
        }

        return Ok();
    }
}
