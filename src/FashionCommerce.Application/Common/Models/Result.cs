namespace FashionCommerce.Application.Common.Models;

/// <summary>
/// Result pattern wrapper. Handlers return this instead of throwing for
/// expected failure cases (invalid credentials, business rule violations),
/// keeping exceptions reserved for genuinely unexpected errors. Controllers
/// map a failed Result to the appropriate HTTP status via the Shared
/// response wrapper.
/// </summary>
public class Result
{
    public bool Succeeded { get; }
    public string[] Errors { get; }

    protected Result(bool succeeded, IEnumerable<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors.ToArray();
    }

    public static Result Success() => new(true, Array.Empty<string>());
    public static Result Failure(params string[] errors) => new(false, errors);
    public static Result Failure(IEnumerable<string> errors) => new(false, errors);
}

public class Result<T> : Result
{
    public T? Value { get; }

    protected Result(bool succeeded, T? value, IEnumerable<string> errors) : base(succeeded, errors)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, Array.Empty<string>());
    public static new Result<T> Failure(params string[] errors) => new(false, default, errors);
    public static new Result<T> Failure(IEnumerable<string> errors) => new(false, default, errors);
}
