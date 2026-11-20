namespace Sentium.Praxis.Features.Workflows;

/// <summary>
/// The run cannot start, for example because its agent or workflow no longer exists. Retrying would not help, so the
/// processor records it as failed at once with <see cref="Exception.Message"/> as the reason.
/// </summary>
internal sealed class UnrunnableWorkflowException : Exception
{
    public UnrunnableWorkflowException()
    {
    }

    public UnrunnableWorkflowException(string message)
        : base(message)
    {
    }

    public UnrunnableWorkflowException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
