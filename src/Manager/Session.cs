using System;
using ShindenAPI.Models;

namespace ShindenAPI;

internal class UserSession
{
    private readonly TimeSpan _lifetime;

    internal UserSession(string id, string name, string hash, UserSearchResult user,
        DateTimeOffset created, TimeSpan lifetime)
    {
        Id = id;
        Name = name;
        Hash = hash;
        User = user;
        Created = created;
        _lifetime = lifetime;
    }

    private DateTimeOffset Created { get; }

    internal string Id { get; }
    internal string Name { get; }
    internal string Hash { get; }
    internal DateTime Expires => Created.Add(_lifetime).UtcDateTime;

    internal UserSearchResult User { get; }

    internal bool IsValid(DateTimeOffset now) => now - Created < _lifetime;
}