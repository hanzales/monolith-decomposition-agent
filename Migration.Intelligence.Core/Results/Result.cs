namespace Migration.Intelligence.Core.Results;

public class Result
{
    public bool IsSuccess { get; protected init; }
    public string ErrorMessage { get; protected init; } = string.Empty;

    public static Result Success()
    {
        return new Result { IsSuccess = true };
    }

    public static Result Failure(string errorMessage)
    {
        return new Result { IsSuccess = false, ErrorMessage = errorMessage };
    }
}
