using ShindenAPI.Models;

namespace ShindenAPI.Queries.Search;

public class User : Search<UserSearchResult>
{
    public User(string query, uint limit = 10) : base("users", query, RequestUri.Parameter("limit", ValidateLimit(limit))) {}

    private static uint ValidateLimit(uint limit) => limit is >= 1 and <= 20
        ? limit : throw new System.ArgumentOutOfRangeException(nameof(limit));
}
