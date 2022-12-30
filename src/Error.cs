using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ShindenAPI;

public class Error
{
    public int Code { get; init; } = 200;
    public string Message { get; init; } = default;

    public bool IsOk() => Code > 199 && Code < 300;

    internal static Error FromException(Exception ex) => new()
        {
            Message = ex.Message,
            Code = 500
        };

    internal static async Task<Error> FromHttpResponseMessage(HttpResponseMessage message) => new()
        {
            Code = (int)message.StatusCode,
            Message = await message.Content.ReadAsStringAsync()
        };
}
