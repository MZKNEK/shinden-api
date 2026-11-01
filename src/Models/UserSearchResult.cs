using System;
using Newtonsoft.Json;

namespace ShindenAPI.Models;

public class UserSearchResult : IShindenUrlAware
{
    private ShindenUrlResolver _resolver = ShindenUrlResolver.Default;
    [JsonProperty("user_id", Required = Required.Always)] public ulong Id { get; init; }
    [JsonProperty("name")] public string Name { get; init; }
    [JsonProperty("rank")] public string Rank { get; init; }
    [JsonProperty("avatar")] public bool HasAvatar { get; init; }
    [JsonIgnore] public string AvatarVersion { get; init; }
    [JsonIgnore] public string AvatarUrl => _resolver.GetUserAvatarUrl(AvatarVersion ?? (HasAvatar ? "1" : "0"), Id);
    [JsonIgnore] public string ProfileUrl => _resolver.GetUserProfileUrl(Id);
    public void UseResolver(ShindenUrlResolver resolver)
        => _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
}
