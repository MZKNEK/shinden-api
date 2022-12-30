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

    public TitleQuick(string name, SearchType type) : base("title", name + TypeToString(type)) {}

    private static string TypeToString(SearchType type) => type switch
    {
        SearchType.Anime => "&accepted_types=Anime&decode=1",
        SearchType.Manga => "&accepted_types=Manga%3BManhua%3BNovel%3BDoujin%3BManhwa%3BOEL%3BOne+Shot&decode=1",
        _ => "&accepted_types=Anime%3Manga%3BManhua%3BNovel%3BDoujin%3BManhwa%3BOEL%3BOne+Shot&decode=1"
    };
}
