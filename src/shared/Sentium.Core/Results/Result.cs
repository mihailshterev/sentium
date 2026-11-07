namespace Sentium.Core.Results;

public enum ResultStatus
{
    Success,
    NotFound,
    Conflict,
    Invalid
}

public readonly record struct Result<T>(ResultStatus Status, T? Value, string? Error)
{
    public bool IsSuccess => Status == ResultStatus.Success;

    public static Result<T> Success(T value) => new(ResultStatus.Success, value, null);

    public static Result<T> NotFound(string? error = null) => new(ResultStatus.NotFound, default, error);

    public static Result<T> Conflict(string error) => new(ResultStatus.Conflict, default, error);

    public static Result<T> Invalid(string error) => new(ResultStatus.Invalid, default, error);
}
