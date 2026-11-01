using System;
using System.Collections.Generic;
using System.Net.Http;
using ShindenAPI.Credentials;
using ShindenAPI.Models;
using Newtonsoft.Json.Linq;

namespace ShindenAPI.Queries.User;

internal class Login : LegacyRequest<LoginResult>
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

    internal override LoginResult Parse(string response)
    {
        var payload = JObject.Parse(response);
        var user = payload["logged_user"];
        var avatar = user?["avatar"]?.ToString();
        return new LoginResult
        {
            User = user is null ? null : new UserSearchResult
            {
                Id = user.Value<ulong>("user_id"), Name = user.Value<string>("name"),
                Rank = user.Value<string>("rank"), HasAvatar = !string.IsNullOrEmpty(avatar) && avatar != "0",
                AvatarVersion = avatar
            },
            Session = payload["session"]?.ToObject<Models.Session>(),
            Hash = payload.Value<string>("hash")
        };
    }

    private HttpContent BuildContent() => new FormUrlEncodedContent(new[]
    {
        new KeyValuePair<string, string>("username", _auth.Nickname),
        new KeyValuePair<string, string>("password", _auth.Password)
    });
}
