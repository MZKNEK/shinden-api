using Newtonsoft.Json;

namespace ShindenAPI.Models;

public class UserSearchResult
{
    private string _avatarId { get; init; }
    private string _url { get; set; }

    [JsonProperty("user_id")]
    public ulong Id { get; init; }
    [JsonProperty("name")]
    public string Name { get; init; }
    [JsonProperty("rank")]
    public string Rank { get; init; }
    [JsonProperty("avatar")]
    public string AvatarUrl
    {
        get => _url ??= Shinden.GetUserAvatarUrl(_avatarId, Id);
        init => _avatarId = value;
    }
}
