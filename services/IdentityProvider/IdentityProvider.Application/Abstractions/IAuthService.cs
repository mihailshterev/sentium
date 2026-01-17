using IdentityProvider.Core.Users;

namespace IdentityProvider.Application.Abstractions;

public interface IAuthService
{
    Task<SystemUser> AuthenticateAsync(string email, string password, CancellationToken ct);
    Task<SystemUser> RegisterAsync(string email, string password, CancellationToken ct);
}
