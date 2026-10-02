using System;
using Newtonsoft.Json;

namespace ShindenAPI.Models;

public class CharacterSearchResult : IShindenUrlAware
{
    private string _picId { get; init; }
    private string _url { get; set; }
    private ShindenUrlResolver _urlResolver = ShindenUrlResolver.Default;

    [JsonProperty("character_id")]
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
        get => _url ??= _urlResolver.GetPictureUrl(_picId);
        init => _picId = value;
    }
    public string CharacterUrl => _urlResolver.GetCharacterUrl(Id);

    public void UseResolver(ShindenUrlResolver resolver)
    {
        _urlResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _url = null;
    }
}
