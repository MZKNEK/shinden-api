using System;
using ShindenAPI.Models;

namespace ShindenAPI;

internal class UserSession
{
    internal UserSession(string Id, string Name, string Hash, UserSearchResult User)
    {
        this.Id = Id;
        this.Name = Name;
        this.Hash = Hash;
        this.User = User;
        Created = DateTime.Now;
    }

    private DateTime Created { get; set; }

    internal string Id { get; private set; }
    internal string Name { get; private set; }
    internal string Hash { get; private set; }
    internal DateTime Expires => Created.AddMinutes(90);

    internal UserSearchResult User { get; private set; }

    internal bool IsValid() => (DateTime.Now - Created).Minutes < 90;
}