using System;
using Newtonsoft.Json;

namespace ShindenAPI.Models;

public class TitleSearchResult : IShindenUrlAware
{
    private string _picId { get; init; }
    private string _url { get; set; }
    private ShindenUrlResolver _urlResolver = ShindenUrlResolver.Default;

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
        get => _url ??= _urlResolver.GetPictureUrl(_picId);
        init => _picId = value;
    }
    public string TitleUrl => _urlResolver.GetTitleUrl(Id);

    public void UseResolver(ShindenUrlResolver resolver)
    {
        _urlResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _url = null;
    }
}
