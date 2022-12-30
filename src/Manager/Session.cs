using System;

namespace ShindenAPI;

internal class UserSession
{
    internal UserSession(string Id, string Name, string Hash)
    {
        this.Id = Id;
        this.Name = Name;
        this.Hash = Hash;
        Created = DateTime.Now;
    }

    private DateTime Created { get; set; }

    internal string Id { get; private set; }
    internal string Name { get; private set; }
    internal string Hash { get; private set; }
    internal DateTime Expires => Created.AddMinutes(90);

    internal void Renew(UserSession session)
    {
        Id = session.Id;
        Name = session.Name;
        Hash = session.Hash;
        Created = DateTime.Now;
    }

    internal bool IsValid() => (DateTime.Now - Created).Minutes < 90;
}