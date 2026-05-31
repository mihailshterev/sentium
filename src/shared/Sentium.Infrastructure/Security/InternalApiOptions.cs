namespace Sentium.Infrastructure.Security;

public sealed class InternalApiOptions
{
    public const string SectionName = "InternalApi";
    public string? ApiKey { get; set; }
}
