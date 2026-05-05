using System;
using System.Collections.Generic;
using System.Text;

namespace Sentium.Identity.Core.Users;

public sealed class SystemUser
{
    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public bool IsActive { get; private set; }

    public SystemUser(Guid id, string email, bool isActive)
    {
        Id = id;
        Email = email;
        IsActive = isActive;
    }

    public SystemUser(Guid id, string email)
    {
        Id = id;
        Email = email;
    }

    public void EnsureCanLogin()
    {
        if (!IsActive)
            throw new ArgumentException();
    }
}
