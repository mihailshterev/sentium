namespace Sentinel.Core.Events;

public sealed record NetworkEventRecord(
    Guid Id,
    string Source,
    string Action,
    DateTime Timestamp,
    string OrigH,
    string RespH,
    string Proto,
    string Service,
    string MlScore
);
