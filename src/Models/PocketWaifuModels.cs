using System.Collections.Generic;
using Newtonsoft.Json;

namespace ShindenAPI.Models;

public class PwCardIds
{
    [JsonProperty("ids")] public List<ulong> Ids { get; init; }
    [JsonProperty("total")] public long Total { get; init; }
    [JsonProperty("truncated")] public bool Truncated { get; init; }
}

public class PwTags
{
    [JsonProperty("status")] public string Status { get; init; }
    [JsonProperty("items")] public List<PwTag> Items { get; init; }
}

public class PwGenres
{
    [JsonProperty("items")] public List<IdName> Items { get; init; }
    [JsonProperty("dere")] public List<IdName> Dere { get; init; }
}

public class PwCardDetails
{
    [JsonProperty("status")] public string Status { get; init; }
    [JsonProperty("card")] public PwCard Card { get; init; }
    [JsonProperty("tags")] public List<PwTag> Tags { get; init; }
    [JsonProperty("owner")] public IdName Owner { get; init; }
    [JsonProperty("efekty")] public PwEffects Effects { get; init; }
}

public class PwEffects
{
    [JsonProperty("glowna")] public string Main { get; init; }
    [JsonProperty("s")] public string S { get; init; }
    [JsonProperty("ss")] public string SS { get; init; }
    [JsonProperty("sss")] public string SSS { get; init; }
}

public class ApiHealth
{
    public string Response { get; init; }
}
