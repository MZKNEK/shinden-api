using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using ShindenAPI.Credentials;
using ShindenAPI.Models;

namespace ShindenAPI.Test;

[TestClass]
public class LegacyApiTests
{
    private static Shinden CreateApi(HttpMessageHandler handler, bool user = false,
        string legacyUri = "https://php.shinden.test/", Func<DateTimeOffset>? clock = null,
        ILogger<Shinden>? logger = null) => new(config => config
            .WithUri("https://new.shinden.test/")
            .WithAuth(new Auth { Token = "shk_new", UserAgent = "new-agent" })
            .WithLegacyUri(legacyUri)
            .WithLegacyAuth(user
                ? new UserAuth { Token = "old&key=value", UserAgent = "php-agent", Nickname = "user", Password = "secret&password", Marmolade = "x-legacy" }
                : new Auth { Token = "old&key=value", UserAgent = "php-agent", Marmolade = "x-legacy" })
            .WithClock(clock ?? (() => DateTimeOffset.UtcNow))
            .WithSessionLifetime(TimeSpan.FromMinutes(1))
            .WithMaxRetries(2).WithRetryDelay(TimeSpan.Zero).Build(), logger!, handler: handler);

    [TestMethod]
    public async Task RestoredMethodsUsePhpRoutesAndOldKey()
    {
        using var handler = new LegacyHandler();
        using var api = CreateApi(handler);
        async Task Expect<T>(Task<ErrorOr<T>> task, string path, string method, string? form = null) where T : class
        {
            var result = await task;
            result.IsOk().Should().BeTrue(result.GetMessage());
            var request = handler.Requests.Last();
            request.Uri.Host.Should().Be("php.shinden.test");
            request.Uri.AbsolutePath.Should().Be(path);
            request.Uri.Query.Should().Be("?api_key=old%26key%3Dvalue");
            request.Method.Should().Be(method);
            request.Authorization.Should().BeNull();
            request.Marmolade.Should().Be("marmolada");
            request.Body.Should().Be(form);
        }

        await Expect(api.Users.FavouriteCharactersAsync(42), "/api/user/42/fav-chars", "GET");
        await Expect(api.Users.ChangeTitleStatusAsync(42, 11, TitleListStatus.OnHold), "/api/userlist/42/series/11", "POST", "status=hold");
        await Expect(api.Users.ChangeTitleStatusAsync(42, 11, "in progress"), "/api/userlist/42/series/11", "POST", "status=in+progress");
        await Expect(api.Users.RemoveTitleStatusAsync(42, 11), "/api/userlist/42/series/11", "DELETE");
        await Expect(api.Users.IncreaseWatchedEpisodeAsync(42, 11), "/api/userlist/42/increase-watched/11", "POST");
        await Expect(api.Users.RateAnimeAsync(11, AnimeRatingCategory.Characters, 8), "/api/anime/11/rate", "POST", "type=titlecahracters&value=8");
        await Expect(api.Users.RateMangaAsync(11, MangaRatingCategory.Art, 8), "/api/manga/11/rate", "POST", "type=lines&value=8");
        await Expect(api.Users.RateAnimeAsync(11, "total", 9), "/api/anime/11/rate", "POST", "type=total&value=9");
        await Expect(api.Users.RateMangaAsync(11, "total", 9), "/api/manga/11/rate", "POST", "type=total&value=9");
        await Expect(api.Users.AddToFavouritesAsync(b => b.WithUserId(42).WithCharacterId(11).Build()), "/api/userlist/42/fav", "POST", "id=character-11");
        await Expect(api.Users.RemoveFromFavouritesAsync(b => b.WithUserId(42).WithStaffId(11).Build()), "/api/userlist/42/fav", "DELETE", "id=staff-11");
        var fav = new Queries.Builder.FavouriteQueryConfigBuilder().WithUserId(42).WithTitleId(11).Build();
        await Expect(api.Users.AddToFavouritesAsync(fav), "/api/userlist/42/fav", "POST", "id=title-11");
        await Expect(api.Users.RemoveFromFavouritesAsync(fav), "/api/userlist/42/fav", "DELETE", "id=title-11");
        await Expect(api.Titles.EpisodeRangeAsync(11), "/api/episode/11/range", "GET");
        await Expect(api.Titles.RecommendationsAsync(11), "/api/title/11/recommendations", "GET");
        await Expect(api.Titles.ReviewsAsync(11), "/api/title/11/reviews", "GET");
        await Expect(api.Titles.RelationsAsync(11), "/api/title/11/related", "GET");
        await Expect(api.People.StaffInfoAsync(11), "/api/staff/11", "GET");
        await Expect(api.Characters.ConnectedIdsAsync(), "/api/character/connected", "GET");
        await Expect(api.Characters.AnimeIdsAsync(), "/api/character/in-anime", "GET");
        await Expect(api.Characters.MangaIdsAsync(), "/api/character/in-manga", "GET");
        await Expect(api.Characters.MangaFlatListAsync(), "/api/character/flat-list-book", "GET");
    }

    [DataTestMethod]
    [DataRow("https://php.shinden.test/api")]
    [DataRow("https://php.shinden.test/api/")]
    [DataRow("https://php.shinden.test/")]
    public async Task AskAsyncRoutesLegacyQueriesAndNormalizesHistoricalApiBase(string uri)
    {
        using var handler = new LegacyHandler();
        using var api = CreateApi(handler, legacyUri: uri);
        (await api.AskAsync(new Queries.Title.Reviews(11))).IsOk().Should().BeTrue();
        handler.Requests.Single().Uri.AbsolutePath.Should().Be("/api/title/11/reviews");
        handler.Requests.Single().Authorization.Should().BeNull();
    }

    [TestMethod]
    public async Task LoginSessionAndCredentialsNeverReachNewEndpoints()
    {
        using var handler = new LegacyHandler();
        using var api = CreateApi(handler, user: true, legacyUri: "https://new.shinden.test/");
        var user = (await api.Users.LoginResultAsync()).Get();
        user.Id.Should().Be(42);
        user.AvatarUrl.Should().EndWith("/42.jpg?v18");
        api.Users.IsValidSession().Should().BeTrue();
        (await api.Users.ChangeTitleStatusAsync(42, 11, TitleListStatus.Completed)).IsOk().Should().BeTrue();
        (await api.Search.UsersAsync("nick")).IsOk().Should().BeTrue();
        (await api.Titles.InfoAsync(11)).IsOk().Should().BeTrue();
        (await api.Users.LoginAsync()).Id.Should().Be(42);

        handler.Requests.Count(r => r.Uri.AbsolutePath == "/api/user/login").Should().Be(1);
        var login = handler.Requests.First();
        login.Body.Should().Be("username=user&password=secret%26password");
        login.Cookie.Should().BeNull();
        var mutation = handler.Requests.Single(r => r.Uri.AbsolutePath == "/api/userlist/42/series/11");
        mutation.Cookie.Should().Be("name=session-name; id=session-id");
        foreach (var request in handler.Requests.Where(r => !r.Uri.AbsolutePath.StartsWith("/api/")))
        {
            request.Authorization.Should().Be("Bearer shk_new");
            request.Uri.Query.Should().NotContain("api_key").And.NotContain("old");
            request.Cookie.Should().BeNull();
            request.Marmolade.Should().BeNull();
        }
    }

    [TestMethod]
    public async Task ReconfigureKeepsPhpSessionWhenCredentialsDoNotChange()
    {
        using var handler = new LegacyHandler();
        using var api = CreateApi(handler, user: true);
        (await api.Users.ChangeTitleStatusAsync(42, 11, TitleListStatus.Completed)).IsOk().Should().BeTrue();

        api.Reconfigure(builder => builder.WithMaxRetries(0).WithRequestTimeout(TimeSpan.FromSeconds(5)).Build());

        api.Users.IsValidSession().Should().BeTrue();
        (await api.Users.ChangeTitleStatusAsync(42, 11, TitleListStatus.Completed)).IsOk().Should().BeTrue();
        handler.Requests.Count(r => r.Uri.AbsolutePath == "/api/user/login").Should().Be(1);
    }

    [TestMethod]
    public async Task ReconfigureWithNewPhpCredentialsDropsSessionAndLogsInAgain()
    {
        using var handler = new LegacyHandler();
        var logger = new SecretLogger();
        using var api = CreateApi(handler, user: true, logger: logger);
        (await api.Users.ChangeTitleStatusAsync(42, 11, TitleListStatus.Completed)).IsOk().Should().BeTrue();

        api.Reconfigure(builder => builder.WithLegacyAuth(new UserAuth
        {
            Token = "new-old-key", UserAgent = "php-agent", Nickname = "other", Password = "other-password", Marmolade = "x-legacy"
        }).Build());

        api.Users.IsValidSession().Should().BeFalse();
        (await api.Users.ChangeTitleStatusAsync(42, 11, TitleListStatus.Completed)).IsOk().Should().BeTrue();
        var logins = handler.Requests.Where(r => r.Uri.AbsolutePath == "/api/user/login").ToArray();
        logins.Should().HaveCount(2);
        logins[1].Body.Should().Be("username=other&password=other-password");
        logins[1].Uri.Query.Should().Contain("api_key=new-old-key");
        // Secrets of the previous configuration stay redacted as well.
        logger.Messages.Should().NotContain(message => message.Contains("secret&password") || message.Contains("new-old-key")
            || message.Contains("other-password"));
    }

    [TestMethod]
    public async Task LoginUsesReturnedCookieNamesAndKeepsThemOffNewApiRequests()
    {
        using var handler = new LegacyHandler
        {
            LoginCookies = new[] { "jwtCookie=modern-token-secret; Path=/; Secure; HttpOnly",
                "sess_shinden=modern-session-secret; Path=/api; Secure; HttpOnly" }
        };
        using var api = CreateApi(handler, user: true, legacyUri: "https://new.shinden.test/");
        (await api.Users.LoginResultAsync()).IsOk().Should().BeTrue();
        (await api.Users.RateAnimeAsync(11, AnimeRatingCategory.Total, 8)).IsOk().Should().BeTrue();
        var mutation = handler.Requests.Last();
        mutation.Cookie.Should().Contain("jwtCookie=modern-token-secret").And.Contain("sess_shinden=modern-session-secret");
        mutation.Cookie.Should().NotContain("name=session-name").And.NotContain("id=session-id");
        (await api.Search.UsersAsync("nick")).IsOk().Should().BeTrue();
        handler.Requests.Last().Cookie.Should().BeNull();
    }

    [TestMethod]
    public async Task ReturnedSessionCookieValuesAreRedactedFromLogs()
    {
        var logger = new SecretLogger();
        using var handler = new LegacyHandler
        {
            LoginCookies = new[] { "jwtCookie=modern-token-secret; Path=/; Secure; HttpOnly" },
            EchoMutation = true
        };
        using var api = CreateApi(handler, user: true, logger: logger);
        (await api.Users.RateAnimeAsync(11, AnimeRatingCategory.Total, 8)).IsError().Should().BeTrue();
        logger.Messages.Should().NotContain(message => message.Contains("modern-token-secret"));
    }

    [TestMethod]
    public async Task ModernRequestsDoNotAttemptPhpLoginEvenWhenLegacyLoginFails()
    {
        using var handler = new LegacyHandler { LoginStatus = HttpStatusCode.Unauthorized };
        using var api = CreateApi(handler, user: true);
        (await api.Search.UsersAsync("nick")).IsOk().Should().BeTrue();
        handler.Requests.Should().ContainSingle();
        var legacy = await api.Users.ChangeTitleStatusAsync(42, 11, TitleListStatus.Completed);
        legacy.GetCode().Should().Be(401);
        handler.Requests.Should().NotContain(r => r.Uri.AbsolutePath == "/api/userlist/42/series/11");
        api.Users.IsValidSession().Should().BeFalse();
    }

    [TestMethod]
    public async Task MissingOldKeyDoesNotFallBackToNewApplicationToken()
    {
        using var handler = new LegacyHandler();
        using var api = new Shinden(new Uri("https://new.shinden.test/"),
            new Auth { Token = "shk_new", UserAgent = "test" }, handler: handler);
        var result = await api.Users.RateAnimeAsync(11, AnimeRatingCategory.Total, 8);
        result.Error.Type.Should().Be(ErrorType.Configuration);
        handler.Requests.Should().BeEmpty();
        (await api.Users.LoginResultAsync()).Error.Type.Should().Be(ErrorType.Authentication);
        (await api.Search.UsersAsync("nick")).IsOk().Should().BeTrue();
    }

    [TestMethod]
    public async Task HistoricalUserAuthConstructorStillSupportsPhpLogin()
    {
        using var handler = new LegacyHandler();
        using var api = new Shinden(new Uri("https://php.shinden.test/api"),
            new UserAuth { Token = "old-key", UserAgent = "test", Nickname = "user", Password = "password" }, handler: handler);
        (await api.Users.LoginAsync()).Id.Should().Be(42);
        (await api.Search.UsersAsync("nick")).IsOk().Should().BeTrue();
        handler.Requests.Last().Authorization.Should().BeNull();
        handler.Requests.Last().Uri.AbsolutePath.Should().Be("/v1/search/users");
    }

    [TestMethod]
    public async Task ExpiredSessionRelogsInAndConcurrentRequestsShareLogin()
    {
        var now = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        using var handler = new LegacyHandler { DelayLogin = true };
        using var api = CreateApi(handler, user: true, clock: () => now);
        var initial = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => api.Users.FavouriteCharactersAsync(42)));
        initial.Should().OnlyContain(r => r.IsOk());
        handler.Requests.Count(r => r.Uri.AbsolutePath == "/api/user/login").Should().Be(1);
        now = now.AddMinutes(2);
        api.Users.IsValidSession().Should().BeFalse();
        (await api.Users.FavouriteCharactersAsync(42)).IsOk().Should().BeTrue();
        handler.Requests.Count(r => r.Uri.AbsolutePath == "/api/user/login").Should().Be(2);
    }

    [TestMethod]
    public async Task LegacyMutationsAreNeverRetriedOrSentToNewApi()
    {
        using var handler = new LegacyHandler { MutationStatus = HttpStatusCode.ServiceUnavailable };
        using var api = CreateApi(handler);
        (await api.Users.ChangeTitleStatusAsync(42, 11, TitleListStatus.OnHold)).GetCode().Should().Be(503);
        (await api.Users.RemoveFromFavouritesAsync(b => b.WithUserId(42).WithTitleId(11).Build())).GetCode().Should().Be(503);
        handler.Requests.Should().HaveCount(2);
        handler.Requests.Should().OnlyContain(r => r.Uri.Host == "php.shinden.test");
    }

    [TestMethod]
    public async Task InvalidLoginSessionIsRejectedBeforeMutation()
    {
        using var handler = new LegacyHandler { LoginBody = """{"logged_user":{"user_id":42,"avatar":"0"},"session":{"id":"","name":""}}""" };
        using var api = CreateApi(handler, user: true);
        (await api.Users.RateMangaAsync(11, MangaRatingCategory.Total, 9)).Error.Type.Should().Be(ErrorType.Authentication);
        handler.Requests.Should().ContainSingle();
    }

    [TestMethod]
    public async Task PhpSecretsAreScrubbedFromErrorLogs()
    {
        var logger = new SecretLogger();
        using var handler = new LegacyHandler { EchoMutation = true };
        using var api = CreateApi(handler, user: true, logger: logger);
        (await api.Users.RateAnimeAsync(11, AnimeRatingCategory.Total, 8)).IsError().Should().BeTrue();
        logger.Messages.Should().NotBeEmpty();
        foreach (var secret in new[] { "old&key=value", "old%26key%3Dvalue", "secret&password", "session-id", "session-name", "session-hash" })
            logger.Messages.Should().NotContain(message => message.Contains(secret));
    }

    [TestMethod]
    public async Task CancellationDuringLoginPropagatesAndDoesNotSendMutation()
    {
        using var handler = new LegacyHandler { DelayLogin = true };
        using var api = CreateApi(handler, user: true);
        using var cancellation = new CancellationTokenSource();
        var request = api.Users.RateAnimeAsync(11, AnimeRatingCategory.Total, 8, cancellation.Token);
        await handler.LoginStarted.Task;
        cancellation.Cancel();
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () => await request);
        handler.Requests.Should().ContainSingle();
    }

    [TestMethod]
    public void RestoredAuthBuilderRejectsIncompleteCredentials()
    {
        Action build = () => new AuthBuilder().WithUserAgent("test").WithUsername("user").Build();
        build.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void PhpResultModelsKeepTheirNestedFields()
    {
        var staff = Newtonsoft.Json.JsonConvert.DeserializeObject<StaffInfo>("""
        {"id":7,"staff_id":7,"first_name":"Person","biography":{"text":"Biography","content":"Details"},
         "relations":[{"id":1,"character_id":42,"title":"Title"}]}
        """)!;
        staff.Biography.Text.Should().Be("Biography");
        staff.Biography.Content.Should().Be("Details");
        staff.Relations.Single().CharacterId.Should().Be(42);
        var recommendations = Newtonsoft.Json.JsonConvert.DeserializeObject<TitleRecommendations>("""
        {"recommendations":[{"id":1,"author":{"user_id":42,"avatar":"18"},
          "recommended_title":{"title_id":11,"title":"Title","cover":"10"}}]}
        """)!;
        recommendations.Recommendations.Single().Author.Id.Should().Be(42);
        recommendations.Recommendations.Single().RecommendedTitle.Id.Should().Be(11);
        var status = Newtonsoft.Json.JsonConvert.DeserializeObject<TitleStatusAfterChange>("""
        {"status":"ok","series_status":{"watched_status_id":"1","user_id":"42","title_id":"11","watch_status":"hold"}}
        """)!;
        status.TitleStatus.WatchStatus.Should().Be("hold");
        var modified = Newtonsoft.Json.JsonConvert.DeserializeObject<ModificationResult>("""{"updated":1}""")!;
        modified.Updated.Should().Be("1");
    }

    private sealed record Snapshot(Uri Uri, string Method, string? Body, string? Authorization, string? Cookie, string? Marmolade);

    private sealed class LegacyHandler : HttpMessageHandler
    {
        internal ConcurrentQueue<Snapshot> Requests { get; } = new();
        internal HttpStatusCode LoginStatus { get; set; } = HttpStatusCode.OK;
        internal HttpStatusCode MutationStatus { get; set; } = HttpStatusCode.OK;
        internal bool DelayLogin { get; set; }
        internal bool EchoMutation { get; set; }
        internal string[] LoginCookies { get; set; } = Array.Empty<string>();
        internal TaskCompletionSource<bool> LoginStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal string LoginBody { get; set; } = """{"logged_user":{"user_id":42,"name":"user","rank":"Tester","avatar":"18"},"session":{"id":"session-id","name":"session-name"},"hash":"session-hash"}""";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var path = uri.AbsolutePath;
            var snapshot = new Snapshot(uri, request.Method.Method,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
                request.Headers.Authorization?.ToString(), Header(request, "Cookie"), Header(request, "x-legacy"));
            Requests.Enqueue(snapshot);
            if (path == "/api/user/login")
            {
                LoginStarted.TrySetResult(true);
                if (DelayLogin) await Task.Delay(30, cancellationToken);
                var response = Json(LoginStatus, LoginBody);
                foreach (var cookie in LoginCookies) response.Headers.TryAddWithoutValidation("Set-Cookie", cookie);
                return response;
            }
            if (request.Method == HttpMethod.Post || request.Method == HttpMethod.Delete)
                return EchoMutation ? Json(HttpStatusCode.BadRequest, snapshot.Uri + " " + snapshot.Cookie + " secret&password session-hash")
                    : Json(MutationStatus, "{}");
            var body = path switch
            {
                "/compat/php-api/title/11/info" => """{"title":{"title_id":11}}""",
                "/v1/search/users" or "/api/user/42/fav-chars" => "[]",
                "/api/character/connected" or "/api/character/in-anime" or "/api/character/in-manga" => "[1,2]",
                "/api/character/flat-list-book" => """[{"c":"1","p":"2","f":"A","l":"B","t":"Manga","n":"100"}]""",
                _ => "{}"
            };
            return Json(HttpStatusCode.OK, body);
        }

        private static string? Header(HttpRequestMessage request, string name)
            => request.Headers.TryGetValues(name, out var values) ? string.Join("; ", values) : null;
        private static HttpResponseMessage Json(HttpStatusCode code, string body) => new(code) { Content = new StringContent(body) };
    }

    private sealed class SecretLogger : ILogger<Shinden>
    {
        internal List<string> Messages { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception) + exception?.ToString());
    }
}
