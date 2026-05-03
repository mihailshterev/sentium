using Sentium.Identity.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;

namespace Sentium.Identity.Application.Abstractions;

public interface IIdentityService
{
    Task<(IdentityResult Result, ApplicationUser? User)> RegisterUserAsync(RegisterRequest request);
    Task<SignInResult> LoginAsync(string email, string password);
}
