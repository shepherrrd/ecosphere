namespace Ecosphere.Infrastructure.Data.Models;

public class BaseResponse
{
    public bool Status { get; set; }
    public string Message { get; set; } = string.Empty;

    public BaseResponse(bool status, string message)
    {
        Status = status;
        Message = message;
    }

    public static BaseResponse Success(string message = "Success") =>
        new BaseResponse(true, message);

    public static BaseResponse Failure(string message = "Failed") =>
        new BaseResponse(false, message);
}

public class BaseResponse<T>
{
    public bool Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string>? Errors { get; set; }
    public T? Data { get; set; }

    public BaseResponse(bool status, string message)
    {
        Status = status;
        Message = message;
    }

    public BaseResponse(bool status, string message, T? data)
    {
        Status = status;
        Message = message;
        Data = data;
    }

    public BaseResponse(bool status, string message, List<string> errors)
    {
        Status = status;
        Message = message;
        Errors = errors;
    }

    public static BaseResponse<T> Success(T data, string message = "Success") =>
        new BaseResponse<T>(true, message, data);

    public static BaseResponse<T> Failure(string message = "Failed") =>
        new BaseResponse<T>(false, message);

    public static BaseResponse<T> Failure(string message, List<string> errors) =>
        new BaseResponse<T>(false, message, errors);
}

public class ErrorResponse
{
    public bool Status { get; set; } = false;
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? TraceId { get; set; }
}

public class ValidationResultModel
{
    public bool Status { get; set; } = false;
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
}
