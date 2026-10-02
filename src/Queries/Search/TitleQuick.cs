using System.Collections.Generic;
using ShindenAPI.Models;

namespace ShindenAPI.Queries.Search;

public class TitleQuick : Search<TitleSearchResult>
{
    public enum SearchType
    {
        Anime,
        Manga,
        Both
    }

    public TitleQuick(string name, SearchType type) : base("title", name, TypeToParameters(type)) {}

    private static KeyValuePair<string, string>[] TypeToParameters(SearchType type) => type switch
    {
        SearchType.Anime => new[]
        {
            new KeyValuePair<string, string>("accepted_types", "Anime"),
            new KeyValuePair<string, string>("decode", "1")
        },
        SearchType.Manga => new[]
        {
            new KeyValuePair<string, string>("accepted_types", "Manga;Manhua;Novel;Doujin;Manhwa;OEL;One Shot"),
            new KeyValuePair<string, string>("decode", "1")
        },
        _ => new[]
        {
            new KeyValuePair<string, string>("accepted_types", "Anime;Manga;Manhua;Novel;Doujin;Manhwa;OEL;One Shot"),
            new KeyValuePair<string, string>("decode", "1")
        }
    };
}
