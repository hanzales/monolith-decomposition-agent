namespace Migration.Intelligence.Core.Results;

public class ResultT<T> : Result
{
    public T? Value { get; private init; }

    public static ResultT<T> Success(T value)
    {
        return new ResultT<T>
        {
            IsSuccess = true,
            Value = value
        };
    }

    public new static ResultT<T> Failure(string errorMessage)
    {
        return new ResultT<T>
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
