using System;
using ShindenAPI.Models;

namespace ShindenAPI.Queries.Search;

public class TitleQuick : Search<TitleSearchResult>
{
    public enum SearchType { Anime, Manga, Both }

    public TitleQuick(string name, SearchType type = SearchType.Both, uint limit = 10, ulong? ignoreId = null)
        : base("titles", name, RequestUri.Parameter("types", Types(type)),
            RequestUri.Parameter("limit", limit is >= 1 and <= 50 ? limit : throw new ArgumentOutOfRangeException(nameof(limit))),
            RequestUri.Parameter("ignore_id", ignoreId)) {}

    private static string Types(SearchType type) => type switch
    {
        SearchType.Anime => "Anime",
        SearchType.Manga => "Manga;Manhua;Manhwa;One Shot;Webtoon;Doujin;OEL;Novel;Webnovel",
        SearchType.Both => null,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
