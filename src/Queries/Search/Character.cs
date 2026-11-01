using ShindenAPI.Models;

namespace ShindenAPI.Queries.Search;

public class Character : Search<CharacterSearchResult>
{
    public Character(string query, uint limit = 10) : base("characters", query, RequestUri.Parameter("limit", ValidateLimit(limit))) {}

    private static uint ValidateLimit(uint limit) => limit is >= 1 and <= 20
        ? limit : throw new System.ArgumentOutOfRangeException(nameof(limit));
}
