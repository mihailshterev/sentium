namespace AgentRuntime.Core.Dtos;

public sealed record NetworkEventAnalysisRequest(
    string Id,
    string Source,
    string Action,
    string Timestamp,
    string OrigH,
    string RespH,
    string Proto,
    string Service,
    string MlScore);
