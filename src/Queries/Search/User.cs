using ShindenAPI.Models;

namespace ShindenAPI.Queries.Search;

public class User : Search<UserSearchResult>
{
    public User(string username) : base("user", username) {}
}
