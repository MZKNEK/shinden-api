using System;
using Newtonsoft.Json;

namespace ShindenAPI.Models;

public class UserSearchResult : IShindenUrlAware
{
    private string _avatarId { get; init; }
    private string _url { get; set; }
    private ShindenUrlResolver _urlResolver = ShindenUrlResolver.Default;

    [JsonProperty("user_id")]
    public ulong Id { get; init; }
    [JsonProperty("name")]
    public string Name { get; init; }
    [JsonProperty("rank")]
    public string Rank { get; init; }
    [JsonProperty("avatar")]
    public string AvatarUrl
    {
        get => _url ??= _urlResolver.GetUserAvatarUrl(_avatarId, Id);
        init => _avatarId = value;
    }
    public string ProfileUrl => _urlResolver.GetUserProfileUrl(Id);

    public void UseResolver(ShindenUrlResolver resolver)
    {
        _urlResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _url = null;
    }
}
