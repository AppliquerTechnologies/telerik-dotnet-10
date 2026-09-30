using TelerikDemo2.Domain;

namespace TelerikDemo2.Common;

// An empty Field means the error is not tied to a form field.
public sealed record FieldError(string Field, string Message);

public class Result
{
    public List<FieldError> Errors { get; } = new();
    public bool Succeeded => Errors.Count == 0;

    public Result AddError(string field, string message)
    {
        Errors.Add(new FieldError(field, message));
        return this;
    }

    public Result AddViolation(RuleViolation violation)
    {
        if (violation is not null) AddError(violation.Field, violation.Message);
        return this;
    }

    public static Result Ok() => new();
    public static Result Fail(string field, string message) => new Result().AddError(field, message);
}

public sealed class Result<T> : Result
{
    public T Value { get; init; }

    public static Result<T> Ok(T value) => new() { Value = value };
    public static new Result<T> Fail(string field, string message)
    {
        var r = new Result<T>();
        r.AddError(field, message);
        return r;
    }
}
