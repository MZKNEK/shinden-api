using Newtonsoft.Json;

namespace ShindenAPI.Models;

public class ModificationResult
{
    [JsonProperty("updated")]
    public string Updated { get; init; }
}
