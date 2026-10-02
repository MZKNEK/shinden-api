using System;
using System.Net;
using System.Net.Http;

namespace ShindenAPI;

public enum ErrorType
{
    None,
    Http,
    Exception,
    Parsing,
    Timeout,
    Authentication,
    Configuration
}

public class Error
{
    private const int MinSuccessCode = 200;
    private const int MaxSuccessCode = 299;

    public ErrorType Type { get; init; } = ErrorType.None;
    public int Code { get; init; } = MinSuccessCode;
    public string Message { get; init; } = string.Empty;

    public bool IsOk() => Type is ErrorType.None && Code >= MinSuccessCode && Code <= MaxSuccessCode;

    internal static Error FromException(Exception ex, ErrorType type = ErrorType.Exception, int code = 500) => new()
    {
        Message = ex.Message,
        Code = code,
        Type = type
    };

    internal static Error FromMessage(ErrorType type, int code, string message) => new()
    {
        Message = message,
        Code = code,
        Type = type
    };

    internal static Error FromHttpResponse(HttpStatusCode statusCode, string content) => new()
    {
        Code = (int)statusCode,
        Message = content,
        Type = ErrorType.Http
    };
}
