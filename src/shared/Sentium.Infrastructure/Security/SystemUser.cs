namespace Sentium.Infrastructure.Security;

internal sealed class SystemUser : ICurrentUser
{
    public static readonly SystemUser Instance = new();

    private SystemUser() { }

    public Guid? UserId => null;
    public bool IsAuthenticated => false;
    public bool IsSovereign => false;
    public bool IsSystem => true;
}
