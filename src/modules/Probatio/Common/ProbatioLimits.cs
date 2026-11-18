namespace Sentium.Probatio.Common;

internal static class ProbatioLimits
{
    public const int SuiteNameMax = 120;
    public const int SuiteDescriptionMax = 2000;
    public const int CasesPerSuiteMax = 200;
    public const int CaseNameMax = 120;
    public const int PromptMax = 8000;
    public const int TurnTextMax = 8000;
    public const int PriorTurnsMax = 20;
    public const int ExpectedAnswerMax = 8000;
    public const int ToolCallsPerCaseMax = 20;
    public const int ToolNameMax = 128;
    public const int ArgumentsPerToolCallMax = 20;
    public const int ArgumentNameMax = 128;
    public const int CannedResultMax = 16000;
    public const int ChecksPerCaseMax = 10;
    public const int AgentNameMax = 100;
    public const int ModelNameMax = 256;
    public const int ErrorMax = 2000;
    public const int RepetitionsMax = 10;
    public const int BenchmarkModelsMin = 2;
    public const int BenchmarkModelsMax = 6;
    public const int QualityScoreMin = 1;
    public const int QualityScoreMax = 5;

    /// <summary>
    /// A quality Check passes at this Judge score unless it sets its own threshold.
    /// </summary>
    public const int DefaultQualityThreshold = 4;
}
