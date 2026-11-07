using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ShindenAPI.Models;

namespace ShindenAPI.Queries.V1;

public sealed class Anime : GetRequest<CatalogAnime>
{
    public Anime(ulong id) : base($"v1/catalog/anime/{id}") { }
}

public sealed class Character : GetRequest<CatalogCharacter>
{
    public Character(ulong id) : base($"v1/catalog/characters/{id}") { }
}

public sealed class UserStatistics : GetRequest<UserStats>
{
    public UserStatistics(ulong userId) : base($"v1/users/{userId}/stats") { }
}

public sealed class TitlesByIds : GetRequest<List<TitleById>>
{
    public TitlesByIds(IEnumerable<ulong> ids) : base("v1/search/titles/by-ids", Ids(ids)) { }

    internal override List<TitleById> Parse(string response)
    {
        var root = JToken.Parse(response);
        var items = root is JObject obj ? obj["items"] : root;
        return items is JArray array ? array.ToObject<List<TitleById>>()
            : throw new JsonSerializationException("Expected a title array or an object containing an items array. Path 'items'.");
    }

    private static KeyValuePair<string, string> Ids(IEnumerable<ulong> ids)
    {
        if (ids is null) throw new ArgumentNullException(nameof(ids));
        var values = ids.Take(101).ToArray();
        if (values.Length is < 1 or > 100 || values.Any(id => id == 0))
            throw new ArgumentException("Provide between 1 and 100 positive title IDs.", nameof(ids));
        return RequestUri.Parameter("ids", string.Join(",", values));
    }
}

public sealed class Tags : Search.Search<SearchTag>
{
    public Tags(string query, string types = null, uint limit = 10)
        : base("tags", query, RequestUri.Parameter("types", types),
            RequestUri.Parameter("limit", limit is >= 1 and <= 20 ? limit : throw new ArgumentOutOfRangeException(nameof(limit)))) { }
}

/// <summary>Anime season; names match what the API sends and accepts (winter/spring/summer/fall).</summary>
public enum Season
{
    Winter,
    Spring,
    Summer,
    Fall,
    [Obsolete("Use Season.Fall; the API calls this season \"fall\".")] Autumn = Fall
}

internal static class SeasonNames
{
    // Explicit mapping: Enum.ToString() is ambiguous for the Autumn alias.
    internal static string ToApiName(this Season season) => season switch
    {
        Season.Winter => "winter",
        Season.Spring => "spring",
        Season.Summer => "summer",
        Season.Fall => "fall",
        _ => throw new ArgumentOutOfRangeException(nameof(season))
    };
}

public sealed class SeasonTitles : GetRequest<SearchResult>
{
    public SeasonTitles(int year, Season season, string order = "popular", string type = null, uint? limit = null)
        : base(Path(year, season), RequestUri.Parameter("order", order), RequestUri.Parameter("type", type),
            RequestUri.Parameter("limit", limit is null or (>= 1 and <= 2000) ? limit : throw new ArgumentOutOfRangeException(nameof(limit)))) { }

    private static string Path(int year, Season season)
    {
        if (year is < 1900 or > 2100) throw new ArgumentOutOfRangeException(nameof(year));
        return $"v1/search/season/{year}/{season.ToApiName()}";
    }
}

public sealed class Health : GetRequest<ApiHealth>
{
    public Health() : base("health") { }
    internal override ApiHealth Parse(string response) => new() { Response = response };
}
