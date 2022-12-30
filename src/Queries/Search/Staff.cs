using ShindenAPI.Models;

namespace ShindenAPI.Queries.Search;

public class Staff : Search<StaffSearchResult>
{
    public Staff(string name) : base("staff", name) {}
}
