namespace Sentium.Probatio.Common;

internal static class ErrorText
{
    /// <summary>
    /// Shortens an error message to fit the error columns.
    /// </summary>
    public static string Fit(string message)
        => message.Length <= ProbatioLimits.ErrorMax ? message : message[..ProbatioLimits.ErrorMax];
}
