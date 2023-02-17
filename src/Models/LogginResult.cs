using Newtonsoft.Json;

namespace ShindenAPI.Models;

public class LogginResult
{
    [JsonProperty("logged_user")]
    public UserSearchResult User { get; init; }
    [JsonProperty("session")]
    public Session Session { get; init; }
    [JsonProperty("hash")]
    public string Hash { get; init; }
}
