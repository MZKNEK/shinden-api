using System;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
    /// <summary>Machine-readable v1 code, for example api_key_scope or too_fast.</summary>
    public string ApiCode { get; init; }
    public string Field { get; init; }
    public int? RetryAfterSeconds { get; init; }
    public string RawResponse { get; init; }
    /// <summary>Original exception type for parsing, timeout and transport failures.</summary>
    public string ExceptionType { get; init; }
    public string JsonPath { get; init; }
    public int? JsonLineNumber { get; init; }
    public int? JsonLinePosition { get; init; }

    public bool IsOk() => Type is ErrorType.None && Code >= MinSuccessCode && Code <= MaxSuccessCode;

    internal static Error FromException(Exception ex, ErrorType type = ErrorType.Exception, int code = 500) => new()
    {
        Message = ex.Message,
        Code = code,
        Type = type,
        ExceptionType = ex.GetType().FullName,
        JsonPath = ex is JsonReaderException reader ? reader.Path : (ex as JsonSerializationException)?.Path,
        JsonLineNumber = ex is JsonReaderException readerLine ? readerLine.LineNumber : (ex as JsonSerializationException)?.LineNumber,
        JsonLinePosition = ex is JsonReaderException readerPosition ? readerPosition.LinePosition : (ex as JsonSerializationException)?.LinePosition
    };

    internal static Error FromMessage(ErrorType type, int code, string message) => new()
    {
        Message = message,
        Code = code,
        Type = type
    };

    internal static Error FromHttpResponse(HttpStatusCode statusCode, string content, int? retryAfterSeconds = null)
    {
        JObject payload = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(content)) payload = JObject.Parse(content);
        }
        catch (JsonException) { /* Empty compatibility responses and non-JSON proxy errors are valid errors too. */ }

        return new Error
        {
            Code = (int)statusCode,
            // PHP API errors carry text in "status"; Cloudflare error pages put the numeric code there.
            Message = Text(payload, "message") ?? StringText(payload, "status")
                ?? Text(payload, "detail") ?? Text(payload, "title") ?? content,
            ApiCode = Text(payload, "code") ?? Text(payload, "error_name"),
            Field = Text(payload, "field"),
            RetryAfterSeconds = retryAfterSeconds ??
                (int.TryParse(Text(payload, "retry_after"), out var seconds) ? seconds : (int?)null),
            RawResponse = content,
            Type = ErrorType.Http
        };
    }

    private static string Text(JObject payload, string name)
        => payload?[name] is JValue value ? value.ToString() : null;

    private static string StringText(JObject payload, string name)
        => payload?[name] is JValue { Type: JTokenType.String } value ? value.ToString() : null;
}
