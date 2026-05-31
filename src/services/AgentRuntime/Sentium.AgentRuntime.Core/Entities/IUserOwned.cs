namespace Sentium.AgentRuntime.Core.Entities;

/// <summary>
/// Marks an entity as owned by a specific user. The data layer stamps <see cref="UserId"/> on
/// insert and applies a global query filter so each user only sees their own rows
/// (Sovereigns and system processes bypass the filter).
/// </summary>
public interface IUserOwned
{
    Guid UserId { get; set; }
}
