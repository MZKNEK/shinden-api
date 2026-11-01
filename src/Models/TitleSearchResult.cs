using System;
using Newtonsoft.Json;

namespace ShindenAPI.Models;

public class TitleSearchResult : IShindenUrlAware
{
    private ShindenUrlResolver _resolver = ShindenUrlResolver.Default;
    [JsonProperty("title_id", Required = Required.Always)] public ulong Id { get; init; }
    [JsonProperty("title")] public string Name { get; init; }
    [JsonProperty("type")] public string Type { get; init; }
    [JsonProperty("kind")] public string Kind { get; init; }
    [JsonProperty("title_status")] public string Status { get; init; }
    [JsonProperty("cover")] public ulong? CoverId { get; init; }
    [JsonProperty("premiere_date")] public string PremiereDate { get; init; }
    [JsonProperty("other_to")] public string OtherTitle { get; init; }
    [JsonProperty("title_url")] public string TitleUrl { get; init; }
    [JsonIgnore] public string PictureUrl => _resolver.GetPictureUrl(CoverId?.ToString());
    public void UseResolver(ShindenUrlResolver resolver)
        => _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
}
