namespace Sentium.Tests.Integration.Common;

/// <summary>
/// The monolith with its real authentication schemes (session cookie, Device token, internal key) instead of
/// <see cref="TestAuthHandler"/>, for tests about who is turned away.
/// </summary>
public sealed class RealAuthWebApplicationFactory : MonolithWebApplicationFactory
{
    protected override bool AuthenticateEveryRequest => false;
}
