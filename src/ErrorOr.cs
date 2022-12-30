using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ShindenAPI;

public class ErrorOr<TSelf> where TSelf : class
{
    public TSelf Value { get; init; } = default!;
    public Error Error { get; init; } = new Error();

    public bool IsOk() => Error.IsOk();
    public int GetCode() => Error.Code;
    public string GetMessage() => Error.Message;

    public void Deconstruct(out TSelf _self) => _self = Value;

    public void Deconstruct(out TSelf _self, out Error _error)
    {
        _self = Value;
        _error = Error;
    }

    internal static ErrorOr<TNew> From<TNew>(TNew obj) where TNew : class => new() { Value = obj };

    internal static ErrorOr<TSelf> FromException(Exception ex) => new() { Error = Error.FromException(ex) };

    internal static async Task<ErrorOr<TSelf>> FromHttpResponseMessage(HttpResponseMessage message) => new()
        { Error = await Error.FromHttpResponseMessage(message) };
}
