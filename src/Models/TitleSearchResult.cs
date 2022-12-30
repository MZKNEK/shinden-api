using Newtonsoft.Json;

namespace ShindenAPI.Models;

public class TitleSearchResult
{
    private string _picId { get; init; }
    private string _url { get; set; }

    [JsonProperty("title_id")]
    public ulong Id { get; init; }
    [JsonProperty("type")]
    public string Type { get; init; }
    [JsonProperty("title")]
    public string Name { get; init; }
    [JsonProperty("title_status")]
    public string Status { get; init; }
    [JsonProperty("cover")]
    public string PictureUrl
    {
        get => _url ??= Shinden.GetPictureUrl(_picId);
        init => _picId = value;
    }
}
