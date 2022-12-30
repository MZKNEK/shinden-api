using Newtonsoft.Json;

namespace ShindenAPI.Models;

public class StaffSearchResult
{
    private string _picId { get; init; }
    private string _url { get; set; }

    [JsonProperty("staff_id")]
    public ulong Id { get; init; }
    [JsonProperty("first_name")]
    public string FirstName { get; init; }
    [JsonProperty("last_name")]
    public string LastName { get; init; }
    [JsonProperty("all_names")]
    public string OtherNames { get; init; }
    [JsonProperty("picture")]
    public string PictureUrl
    {
        get => _url ??= Shinden.GetPictureUrl(_picId);
        init => _picId = value;
    }
}
