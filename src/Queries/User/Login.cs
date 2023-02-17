using System;
using System.Net.Http;
using System.Text;
using ShindenAPI.Credentials;
using ShindenAPI.Models;

namespace ShindenAPI.Queries.User;

internal class Login : Request<LogginResult>
{
    private readonly UserAuth _auth;

    internal Login(UserAuth auth) => _auth = auth;

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        RequestUri = new Uri(uri, "api/user/login?api_key=" + token),
        Method = HttpMethod.Post,
        Content = BuildContent()
    };

    internal override bool RequireHttps() => true;

    private HttpContent BuildContent() => new StringContent(
        $"username={_auth.Nickname}&password={_auth.Password}",
        Encoding.UTF8, "application/x-www-form-urlencoded");
}
