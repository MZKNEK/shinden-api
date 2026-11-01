using System;
using Newtonsoft.Json;

namespace ShindenAPI.Models;

public class StaffSearchResult : IShindenUrlAware
{
    private ShindenUrlResolver _resolver = ShindenUrlResolver.Default;
    [JsonProperty("staff_id", Required = Required.Always)] public ulong Id { get; init; }
    [JsonProperty("character_id")] public ulong? CharacterId { get; init; }
    [JsonProperty("first_name")] public string FirstName { get; init; }
    [JsonProperty("last_name")] public string LastName { get; init; }
    [JsonProperty("all_names")] public string OtherNames { get; init; }
    [JsonProperty("picture")] public ulong? PictureId { get; init; }
    [JsonProperty("title")] public string Title { get; init; }
    [JsonProperty("dokladnie")] public bool IsExactMatch { get; init; }
    [JsonIgnore] public string PictureUrl => _resolver.GetPictureUrl(PictureId?.ToString());
    [JsonIgnore] public string StaffUrl => _resolver.GetStaffUrl(Id);
    public void UseResolver(ShindenUrlResolver resolver)
        => _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
}
