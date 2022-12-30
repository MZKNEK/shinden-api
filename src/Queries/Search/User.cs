using System;
using System.Collections.Generic;
using System.Net.Http;
using ShindenAPI.Models;

namespace ShindenAPI.Queries.Search;

public class User : Request<List<UserSearchResult>>
{
    private readonly string _username;

    public User(string username) => _username = username;

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(uri, "api/user/search?query=" + _username)
        };
}
