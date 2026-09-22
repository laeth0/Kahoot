namespace Kahoot.Application.Common.Results;

public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        if (isSuccess && value is null)
        {
            throw new InvalidOperationException("A successful result must provide a non-null value.");
        }

        _value = value;
    }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failing result cannot be accessed.");
}
