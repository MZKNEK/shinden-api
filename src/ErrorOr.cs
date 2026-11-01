using System;
using System.Net;
using System.Net.Http;

namespace ShindenAPI;

/// <summary>
/// A lightweight, allocation-free result type carrying either a successful value or an
/// <see cref="Error"/>. Implemented as a <see langword="readonly struct"/> to avoid a heap
/// allocation on every request.
/// </summary>
public readonly struct ErrorOr<TSelf> where TSelf : class
{
    private ErrorOr(TSelf value, Error error)
    {
        Value = value;
        Error = error;
    }

    public TSelf Value { get; }
    public Error Error { get; }

    public bool IsOk() => Value is not null && Error is not null && Error.IsOk();
    public bool IsError() => !IsOk();
    public int GetCode() => Error?.Code ?? 500;
    public string GetMessage() => Error?.Message ?? string.Empty;

    public TResult Match<TResult>(Func<TSelf, TResult> onSuccess, Func<Error, TResult> onError)
    {
        if (onSuccess is null)
            throw new ArgumentNullException(nameof(onSuccess));
        if (onError is null)
            throw new ArgumentNullException(nameof(onError));

        return IsOk() ? onSuccess(Value) : onError(Error);
    }

    public TSelf Get()
    {
        if (!IsOk())
            throw new InvalidOperationException($"The operation failed with code {GetCode()}: {GetMessage()}");

        return Value;
    }

    public bool TryGet(out TSelf value)
    {
        if (IsOk())
        {
            value = Value;
            return true;
        }

        value = default!;
        return false;
    }

    public bool TryGetError(out Error error)
    {
        if (IsError())
        {
            error = Error;
            return true;
        }

        error = default!;
        return false;
    }

    public void Deconstruct(out TSelf _self, out Error _error)
    {
        _self = Value;
        _error = Error;
    }

    internal static ErrorOr<TNew> From<TNew>(TNew obj) where TNew : class
        => obj is null
            ? throw new ArgumentNullException(nameof(obj))
            : ErrorOr<TNew>.Success(obj);

    internal static ErrorOr<TSelf> FromError(Error error)
        => error is null
            ? throw new ArgumentNullException(nameof(error))
            : Failure(error);

    internal static ErrorOr<TSelf> FromException(
        Exception ex, ErrorType type = ErrorType.Exception, int code = 500)
        => Failure(Error.FromException(ex, type, code));

    internal static ErrorOr<TSelf> FromHttpResponse(HttpStatusCode statusCode, string content, int? retryAfterSeconds = null)
        => Failure(Error.FromHttpResponse(statusCode, content, retryAfterSeconds));

    public static ErrorOr<TSelf> Success(TSelf value)
        => value is null
            ? throw new ArgumentNullException(nameof(value))
            : new ErrorOr<TSelf>(value, new Error());

    public static ErrorOr<TSelf> Failure(Error error)
        => error is null
            ? throw new ArgumentNullException(nameof(error))
            : new ErrorOr<TSelf>(default!, error);
}
