using ShindenAPI.Models;

namespace ShindenAPI.Queries.Search;

public class Character : Search<CharacterSearchResult>
{
    public Character(string name) : base("character", name) {}
}
