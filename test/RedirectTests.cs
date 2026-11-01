using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using ShindenAPI.Credentials;
using ShindenAPI.Models;

namespace ShindenAPI.Test;

[TestClass]
public class RedirectTests
{
    private const string LoginBody = """{"logged_user":{"user_id":42,"name":"user","rank":"Tester","avatar":"18"},"session":{"id":"session-id","name":"session-name"},"hash":"session-hash"}""";

    private static Shinden CreateNewApi(HttpMessageHandler handler) => new(config => config
        .WithUri("https://shinden.test/")
        .WithAuth(new Auth { Token = "shk_new", UserAgent = "test" })
        .WithMaxRetries(0).Build(), handler: handler);

    private static Shinden CreateLegacyApi(HttpMessageHandler handler, bool user = false) => new(config => config
        .WithUri("https://shinden.test/")
        .WithAuth(new Auth { Token = "shk_new", UserAgent = "test" })
        .WithLegacyUri("https://shinden.test/")
        .WithLegacyAuth(user
            ? new UserAuth { Token = "old-key", UserAgent = "test", Nickname = "user", Password = "secret", Marmolade = "x-legacy" }
            : new Auth { Token = "old-key", UserAgent = "test", Marmolade = "x-legacy" })
        .WithMaxRetries(0).Build(), handler: handler);

    [TestMethod]
    public async Task SameSiteRedirectKeepsBearerToken()
    {
        using var handler = new ScriptedHandler(request => request.RequestUri!.Host == "shinden.test"
            ? Redirect(HttpStatusCode.MovedPermanently, "https://www.shinden.test" + request.RequestUri.PathAndQuery)
            : Json("""{"title":{"title_id":42}}"""));
        using var api = CreateNewApi(handler);

        (await api.Titles.InfoAsync(42)).IsOk().Should().BeTrue();

        handler.Requests.Select(r => r.Uri.Host).Should().Equal("shinden.test", "www.shinden.test");
        handler.Requests.Select(r => r.Authorization).Should().Equal("Bearer shk_new", "Bearer shk_new");
    }

    [TestMethod]
    public async Task CrossSiteRedirectDropsCredentials()
    {
        using var handler = new ScriptedHandler(request => request.RequestUri!.Host == "shinden.test"
            ? Redirect(HttpStatusCode.Found, "https://other.test" + request.RequestUri.AbsolutePath)
            : Json(request.RequestUri.AbsolutePath.StartsWith("/api/") ? "[]" : """{"title":{"title_id":42}}"""));
        using (var api = CreateNewApi(handler))
            (await api.Titles.InfoAsync(42)).IsOk().Should().BeTrue();
        using (var api = CreateLegacyApi(handler))
            (await api.Users.FavouriteCharactersAsync(42)).IsOk().Should().BeTrue();

        handler.Requests.Should().HaveCount(4);
        handler.Requests.First().Authorization.Should().Be("Bearer shk_new");
        handler.Requests.Skip(2).First().Marmolade.Should().Be("marmolada");
        foreach (var request in handler.Requests.Where(r => r.Uri.Host == "other.test"))
        {
            request.Authorization.Should().BeNull();
            request.Marmolade.Should().BeNull();
            request.Cookie.Should().BeNull();
            request.UserAgent.Should().NotBeNull();
        }
    }

    [TestMethod]
    public async Task HttpToHttpsUpgradeKeepsBearerToken()
    {
        using var handler = new ScriptedHandler(request => request.RequestUri!.Scheme == Uri.UriSchemeHttp
            ? Redirect(HttpStatusCode.MovedPermanently, "https://shinden.test" + request.RequestUri.PathAndQuery)
            : Json("""{"title":{"title_id":42}}"""));
        using var api = new Shinden(config => config
            .WithUri("http://shinden.test/")
            .WithAuth(new Auth { Token = "shk_new", UserAgent = "test" })
            .WithMaxRetries(0).Build(), handler: handler);

        (await api.Titles.InfoAsync(42)).IsOk().Should().BeTrue();

        handler.Requests.Select(r => r.Uri.Scheme).Should().Equal("http", "https");
        handler.Requests.Select(r => r.Authorization).Should().Equal("Bearer shk_new", "Bearer shk_new");
    }

    [TestMethod]
    public async Task HttpsToHttpRedirectIsNotFollowed()
    {
        using var handler = new ScriptedHandler(_ => Redirect(HttpStatusCode.MovedPermanently, "http://shinden.test/v1/plain"));
        using var api = CreateNewApi(handler);

        var result = await api.Titles.InfoAsync(42);

        result.IsOk().Should().BeFalse();
        result.Error.Code.Should().Be(301);
        handler.Requests.Should().ContainSingle();
    }

    [DataTestMethod]
    [DataRow(302, "GET", null)]
    [DataRow(303, "GET", null)]
    [DataRow(307, "POST", "status=hold")]
    [DataRow(308, "POST", "status=hold")]
    public async Task RedirectedPostFollowsStatusCodeSemantics(int status, string method, string? body)
    {
        using var handler = new ScriptedHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/done")
            ? Json("{}")
            : Redirect((HttpStatusCode)status, "/api/userlist/42/series/11/done"));
        using var api = CreateLegacyApi(handler);

        (await api.Users.ChangeTitleStatusAsync(42, 11, TitleListStatus.OnHold)).IsOk().Should().BeTrue();

        var redirected = handler.Requests.Last();
        redirected.Uri.AbsolutePath.Should().Be("/api/userlist/42/series/11/done");
        redirected.Method.Should().Be(method);
        redirected.Body.Should().Be(body);
        redirected.Marmolade.Should().Be("marmolada");
    }

    [TestMethod]
    public async Task LoginCookiesSetOnRedirectResponseAreKept()
    {
        using var handler = new ScriptedHandler(request =>
        {
            switch (request.RequestUri!.AbsolutePath)
            {
                case "/api/user/login":
                    var redirect = Redirect(HttpStatusCode.Found, "/api/user/login/done");
                    redirect.Headers.TryAddWithoutValidation("Set-Cookie", "jwtCookie=redirect-secret; Path=/; Secure; HttpOnly");
                    return redirect;
                case "/api/user/login/done":
                    return Json(LoginBody);
                default:
                    return Json("{}");
            }
        });
        using var api = CreateLegacyApi(handler, user: true);

        (await api.Users.LoginResultAsync()).IsOk().Should().BeTrue();
        (await api.Users.RateAnimeAsync(11, AnimeRatingCategory.Total, 8)).IsOk().Should().BeTrue();

        handler.Requests.Last().Cookie.Should().Contain("jwtCookie=redirect-secret");
    }

    [TestMethod]
    public async Task RedirectLoopStopsAfterLimit()
    {
        using var handler = new ScriptedHandler(_ => Redirect(HttpStatusCode.Found, "/v1/loop"));
        using var api = CreateNewApi(handler);

        var result = await api.Titles.InfoAsync(42);

        result.IsOk().Should().BeFalse();
        result.Error.Code.Should().Be(302);
        handler.Requests.Should().HaveCount(11);
    }

    private static HttpResponseMessage Redirect(HttpStatusCode status, string location)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent("") };
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private sealed record Snapshot(Uri Uri, string Method, string? Body, string? Authorization,
        string? Cookie, string? Marmolade, string? UserAgent);

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        internal ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        internal ConcurrentQueue<Snapshot> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Enqueue(new Snapshot(request.RequestUri!, request.Method.Method,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
                request.Headers.Authorization?.ToString(), Header(request, "Cookie"),
                Header(request, "x-legacy"), Header(request, "User-Agent")));
            return _respond(request);
        }

        private static string? Header(HttpRequestMessage request, string name)
            => request.Headers.TryGetValues(name, out var values) ? string.Join("; ", values) : null;
    }
}
