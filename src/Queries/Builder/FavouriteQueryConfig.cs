using System.Net.Http;
using System.Text;

namespace Shinden.Queries.Builder;

public class FavouriteQueryConfig
{
    public enum FavType
    {
        Character, Title, Staff
    }

    public required FavType Type { get; init; }
    public required ulong UserId { get; init; }
    public required ulong ObjectId { get; init; }

    internal HttpContent Build()
    {
        return new StringContent($"id={Type.ToString().ToLower()}-{ObjectId}",
            Encoding.UTF8, "application/x-www-form-urlencoded");
    }
}