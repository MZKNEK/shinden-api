using System;
using System.Globalization;
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
    Configuration,
    /// <summary>The request was not sent because of a rate-limit pause or the client-side limit; <see cref="Error.RetryAfter"/> says how long to wait.</summary>
    RateLimit
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
    /// <summary>Wait time from Retry-After (seconds or HTTP date) or the v1 retry_after field, rounded up to whole seconds.</summary>
    public int? RetryAfterSeconds { get; init; }
    /// <summary>Wait time from Retry-After (seconds or HTTP date) or the v1 retry_after field.</summary>
    public TimeSpan? RetryAfter { get; init; }
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

    internal static Error FromRateLimit(TimeSpan retryAfter, string message) => new()
    {
        Message = message,
        Code = 429,
        Type = ErrorType.RateLimit,
        RetryAfter = retryAfter,
        RetryAfterSeconds = CeilingSeconds(retryAfter)
    };

    internal static Error FromHttpResponse(HttpStatusCode statusCode, string content, TimeSpan? retryAfter = null)
    {
        JObject payload = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(content)) payload = JObject.Parse(content);
        }
        catch (JsonException) { /* Empty compatibility responses and non-JSON proxy errors are valid errors too. */ }

        retryAfter ??= BodyRetryAfter(payload);
        return new Error
        {
            Code = (int)statusCode,
            // PHP API errors carry text in "status"; Cloudflare error pages put the numeric code there.
            Message = Text(payload, "message") ?? StringText(payload, "status")
                ?? Text(payload, "detail") ?? Text(payload, "title") ?? content,
            ApiCode = Text(payload, "code") ?? Text(payload, "error_name"),
            Field = Text(payload, "field"),
            RetryAfter = retryAfter,
            RetryAfterSeconds = retryAfter.HasValue ? CeilingSeconds(retryAfter.Value) : null,
            RawResponse = content,
            Type = ErrorType.Http
        };
    }

    private static TimeSpan? BodyRetryAfter(JObject payload)
    {
        // JValue.ToString() follows the current culture, so numbers are read without converting to text.
        double seconds;
        if (payload?["retry_after"] is not JValue value)
            return null;
        if (value.Type is JTokenType.Integer or JTokenType.Float)
            seconds = (double)value;
        else if (value.Type is not JTokenType.String
            || !double.TryParse((string)value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
            return null;
        return seconds >= 0 && seconds <= int.MaxValue ? TimeSpan.FromSeconds(seconds) : null;
    }

    private static int CeilingSeconds(TimeSpan value)
        => (int)Math.Min(int.MaxValue, Math.Max(0, Math.Ceiling(value.TotalSeconds)));

    private static string Text(JObject payload, string name)
        => payload?[name] is JValue value ? value.ToString() : null;

    private static string StringText(JObject payload, string name)
        => payload?[name] is JValue { Type: JTokenType.String } value ? value.ToString() : null;
}
