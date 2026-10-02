using System;
using System.Collections.Generic;
using System.Net.Http;
using ShindenAPI.Credentials;
using ShindenAPI.Models;

namespace ShindenAPI.Queries.User;

internal class Login : Request<LoginResult>
{
    private readonly UserAuth _auth;

    internal Login(UserAuth auth) => _auth = auth;

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        RequestUri = RequestUri.Create(uri, "api/user/login",
            RequestUri.Parameter("api_key", token)),
        Method = HttpMethod.Post,
        Content = BuildContent()
    };

    internal override bool RequireHttps() => true;

    private HttpContent BuildContent() => new FormUrlEncodedContent(new[]
    {
        new KeyValuePair<string, string>("username", _auth.Nickname),
        new KeyValuePair<string, string>("password", _auth.Password)
    });
}
