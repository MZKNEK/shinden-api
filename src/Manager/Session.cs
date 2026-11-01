using System;
using System.Net;
using ShindenAPI.Models;

namespace ShindenAPI;

internal class UserSession
{
    private readonly TimeSpan _lifetime;
    private readonly CookieContainer _cookies;

    internal UserSession(string id, string name, string hash, UserSearchResult user,
        DateTimeOffset created, TimeSpan lifetime, CookieContainer cookies = null, string[] cookieSecrets = null)
    {
        Id = id;
        Name = name;
        Hash = hash;
        User = user;
        Created = created;
        _lifetime = lifetime;
        _cookies = cookies;
        CookieSecrets = cookieSecrets ?? Array.Empty<string>();
    }

    private DateTimeOffset Created { get; }

    internal string Id { get; }
    internal string Name { get; }
    internal string Hash { get; }
    internal DateTime Expires => Created.Add(_lifetime).UtcDateTime;

    internal UserSearchResult User { get; }
    internal string[] CookieSecrets { get; }

    internal string GetCookieHeader(Uri uri)
        => _cookies is null ? $"name={Name}; id={Id}" : _cookies.GetCookieHeader(uri);

    internal bool IsValid(DateTimeOffset now) => now - Created < _lifetime;
}
