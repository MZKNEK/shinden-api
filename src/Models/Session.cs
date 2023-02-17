using Newtonsoft.Json;

namespace ShindenAPI.Models;

public class Session
{
    [JsonProperty("name")]
    public string Name { get; init; }
    [JsonProperty("id")]
    public string Id { get; init; }
}
