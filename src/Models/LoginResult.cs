using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ShindenAPI.Models;

public class LoginResult
{
    [JsonIgnore]
    internal IReadOnlyList<string> SetCookieHeaders { get; set; } = Array.Empty<string>();
    [JsonProperty("logged_user")]
    public UserSearchResult User { get; init; }
    [JsonProperty("session")]
    public Session Session { get; init; }
    [JsonProperty("hash")]
    public string Hash { get; init; }
}
