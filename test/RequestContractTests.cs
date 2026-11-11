using System.Net;
using System.Net.Http;
using ShindenAPI.Credentials;
using ShindenAPI.Queries.Search;
using ShindenAPI.Queries.V1;

namespace ShindenAPI.Test;

[TestClass]
public class RequestContractTests
{
    private static Shinden CreateApi(RecordingHandler handler, string? token = "shk_test", string uri = "https://fake.shinden.test/")
        => new(config => config.WithUri(uri).WithMaxRetries(0)
            .WithAuth(new Auth { Token = token!, UserAgent = "test-agent" }).Build(), handler: handler);

    [TestMethod]
    public async Task AllClientOperationsUseExpectedRoutes()
    {
        using var handler = new RecordingHandler();
        using var api = CreateApi(handler);
        var covered = new HashSet<string>();
        async Task Expect<T>(Task<ErrorOr<T>> operation, string path, string? pattern = null) where T : class
        {
            var result = await operation;
            result.IsOk().Should().BeTrue(result.GetMessage());
            handler.Uri.AbsolutePath.Should().Be(path);
            handler.Method.Should().Be(HttpMethod.Get);
            handler.Body.Should().BeNull();
            handler.Authorization.Should().Be("Bearer shk_test");
            handler.HadApiKeyHeader.Should().BeFalse();
            handler.Query().Should().NotContainKey("api_key");
            covered.Add(pattern ?? path.Replace("/42", "/{id}"));
        }

        handler.Response = """{"user_id":42}""";
        await Expect(api.Users.InfoAsync(42), "/compat/php-api/user/42/info");
        handler.Response = "[]";
        await Expect(api.Users.LastWatchedAsync(42), "/compat/php-api/user/42/last_view");
        await Expect(api.Users.LastReadAsync(42), "/compat/php-api/user/42/last_read");
        await Expect(api.Characters.FlatListAsync(), "/compat/php-api/character/flat-list");
        await Expect(api.Characters.AnimeFlatListAsync(), "/compat/php-api/character/flat-list-anime");
        await Expect(api.Search.UsersAsync("nick"), "/v1/search/users");
        await Expect(api.Search.CharactersAsync("name"), "/v1/search/characters");
        await Expect(api.Search.StaffAsync("name"), "/v1/search/staff");
        await Expect(api.Search.TitlesAsync("name"), "/v1/search/titles");
        await Expect(api.Search.TitlesByIdsAsync(new[] { 42UL, 43UL }), "/v1/search/titles/by-ids");
        await Expect(api.Search.TagsAsync("name"), "/v1/search/tags");
        handler.Response = """{"character_id":42}""";
        await Expect(api.Characters.InfoAsync(42), "/compat/php-api/character/42");
        handler.Response = "{}";
        await Expect(api.Titles.CharactersAsync(42), "/compat/php-api/title/42/characters");
        handler.Response = """{"title":{"title_id":42}}""";
        await Expect(api.Titles.InfoAsync(42), "/compat/php-api/title/42/info");
        handler.Response = "{}";
        await Expect(api.Titles.EpisodesAsync(42), "/compat/php-api/title/42/episodes");
        await Expect(api.Episodes.NewAsync(), "/compat/php-api/episode/new");
        await Expect(api.Episodes.CurrentSeasonAsync(), "/compat/php-api/anime/current-season");
        handler.Query().Should().BeEmpty();
        await Expect(api.Episodes.CurrentSeasonAsync(2026, Season.Spring), "/compat/php-api/anime/current-season");
        handler.Query()["year"].Should().Equal("2026");
        handler.Query()["season"].Should().Equal("spring");
#pragma warning disable CS0618 // the obsolete alias must still send "fall"
        await Expect(api.Episodes.CurrentSeasonAsync(2025, Season.Autumn), "/compat/php-api/anime/current-season");
#pragma warning restore CS0618
        handler.Query()["season"].Should().Equal("fall");
        await Expect(api.Search.AnimeAsync(new AnimeSearchOptionsBuilder().Build()), "/v1/search/browse/anime", "/v1/search/browse/{obj}");
        await Expect(api.Search.MangaAsync(new AnimeSearchOptionsBuilder().Build()), "/v1/search/browse/manga", "/v1/search/browse/{obj}");
        await Expect(api.Search.SeasonAsync(2026, Season.Fall), "/v1/search/season/2026/fall", "/v1/search/season/{year}/{season}");
        handler.Response = """{"id":42}""";
        await Expect(api.Titles.AnimeAsync(42), "/v1/catalog/anime/42");
        await Expect(api.Characters.CatalogAsync(42), "/v1/catalog/characters/42");
        handler.Response = """{"userId":42}""";
        await Expect(api.Users.StatsAsync(42), "/v1/users/42/stats");
        await Expect(api.PocketWaifu.CardsAsync(42), "/v1/users/42/pw/cards");
        await Expect(api.PocketWaifu.CardIdsAsync(42), "/v1/users/42/pw/cards/ids");
        await Expect(api.PocketWaifu.TagsAsync(42), "/v1/users/42/pw/tags");
        await Expect(api.PocketWaifu.GenresAsync(), "/v1/pw/genres");
        await Expect(api.PocketWaifu.CardAsync(42), "/v1/pw/cards/42", "/v1/pw/cards/{wid}");
        await Expect(api.HealthAsync(), "/health");

        covered.Should().HaveCount(28);
    }

    [DataTestMethod]
    [DataRow("https://fake.shinden.test/gateway", "/gateway/compat/php-api/title/42/info")]
    [DataRow("https://fake.shinden.test/gateway/", "/gateway/compat/php-api/title/42/info")]
    [DataRow("https://fake.shinden.test/", "/compat/php-api/title/42/info")]
    public async Task BasePathIsPreservedAndTokenOnlyTravelsInHeader(string baseUri, string expectedPath)
    {
        using var handler = new RecordingHandler { Response = """{"title":{"title_id":42}}""" };
        using var api = CreateApi(handler, "shk_secret", baseUri);
        (await api.Titles.InfoAsync(42)).IsOk().Should().BeTrue();
        handler.Uri.AbsolutePath.Should().Be(expectedPath);
        handler.Uri.ToString().Should().NotContain("shk_secret").And.NotContain("api_key");
        handler.Authorization.Should().Be("Bearer shk_secret");
        handler.Query().Should().NotContainKey("decode");
    }

    [TestMethod]
    public async Task PublicSearchWorksWithoutATokenAndEscapesQuery()
    {
        using var handler = new RecordingHandler { Response = "[]" };
        using var api = CreateApi(handler, token: null);
        (await api.Search.CharactersAsync("名前 & test=1", limit: 20)).IsOk().Should().BeTrue();
        handler.Authorization.Should().BeNull();
        handler.Query()["query"].Should().Equal("名前 & test=1");
        handler.Query()["limit"].Should().Equal("20");
        handler.Query().Should().NotContainKey("test");
    }

    [TestMethod]
    public async Task BrowseUsesGetQueryPaginationAndPhpArrays()
    {
        using var handler = new RecordingHandler { Response = """{"total":3,"items":[]}""" };
        using var api = CreateApi(handler);
        var result = await api.Search.AnimeAsync(builder => builder.WithQuery("Test & title")
            .WithPage(3).WithPageSize(20).WithType("TV").WithType("OVA")
            .WithStatus("Finished Airing").WithSort("title", descending: true).Build());
        result.Get().Total.Should().Be(3);
        handler.Method.Should().Be(HttpMethod.Get);
        handler.Body.Should().BeNull();
        var query = handler.Query();
        query["page"].Should().Equal("3");
        query["limit"].Should().Equal("20");
        query["series_type[]"].Should().Equal("TV", "OVA");
        query["series_status[]"].Should().Equal("Finished Airing");
        query["search"].Should().Equal("Test & title");
        query.Should().NotContainKey("offset").And.NotContainKey("per_page");
    }

    [TestMethod]
    public async Task TitleFiltersIncludeNewMangaTypesAndSupportExclusions()
    {
        using var handler = new RecordingHandler { Response = "[]" };
        using var api = CreateApi(handler);
        await api.Search.TitlesAsync("title", TitleQuick.SearchType.Manga, limit: 50, ignoreId: 99);
        handler.Query()["types"].Single().Should().Contain("Webtoon").And.Contain("Webnovel");
        handler.Query()["ignore_id"].Should().Equal("99");
        handler.Query().Should().NotContainKey("decode");
        await api.Search.TitlesByIdsAsync(new[] { 42UL, 43UL });
        handler.Query()["ids"].Should().Equal("42,43");
    }

    [TestMethod]
    public async Task CompatibilityOptionsUseDocumentedFlagsAndLookupParameters()
    {
        using var handler = new RecordingHandler();
        using var api = CreateApi(handler);
        handler.Response = """{"character_id":42}""";
        await api.AskAsync(new Queries.People.CharacterInfo(42, new Queries.People.CharacterInfoOptions
        {
            Biography = false, Points = false, AnimeOnly = true, Language = "jp"
        }));
        handler.Query()["bio"].Should().Equal("0");
        handler.Query()["points"].Should().Equal("0");
        handler.Query()["table"].Should().Equal("animes");
        handler.Query()["lang"].Should().Equal("jp");
        await api.AskAsync(new Queries.Title.Episodes(42, language: "en"));
        handler.Query()["lang"].Should().Equal("en");
        await api.AskAsync(new Queries.User.UserInfo(42, new Queries.User.UserInfoOptions { ForumId = 99, Name = "Nick & test" }));
        handler.Query()["vb"].Should().Equal("99");
        handler.Query()["name"].Should().Equal("Nick & test");
    }

    [TestMethod]
    public async Task PocketWaifuSharesFiltersAndPreservesOpaqueCursor()
    {
        using var handler = new RecordingHandler();
        using var api = CreateApi(handler);
        var options = new PwCardOptions { Query = "A & B", Ultimate = false, Tradable = true, Tags = "1,2",
            ExcludedTags = "3", TagMode = "and", Limit = 100, Cursor = "a+b/=", Sort = "power", Direction = "desc" };
        await api.PocketWaifu.CardsAsync(42, options);
        var query = handler.Query();
        query["cursor"].Should().Equal("a+b/=");
        query["ultimate"].Should().Equal("0");
        query["tradable"].Should().Equal("1");
        query["limit"].Should().Equal("100");
        query.Should().NotContainKey("cursed");
        await api.PocketWaifu.CardIdsAsync(42, options);
        var idsQuery = handler.Query();
        idsQuery.Should().NotContainKey("cursor").And.NotContainKey("limit");
        foreach (var filter in query.Where(pair => pair.Key is not ("cursor" or "limit")))
            idsQuery[filter.Key].Should().Equal(filter.Value);
    }

    [DataTestMethod]
    [DataRow(401, """{"code":"invalid_api_key","message":"Invalid token"}""", "invalid_api_key", "Invalid token")]
    [DataRow(403, """{"code":"api_key_scope","message":"Missing scope","field":"users"}""", "api_key_scope", "Missing scope")]
    [DataRow(400, """{"status":"wrong api key"}""", null, "wrong api key")]
    [DataRow(404, "", null, "")]
    [DataRow(502, "<html>Proxy error</html>", null, "<html>Proxy error</html>")]
    [DataRow(502, """{"title":"Error 502: Bad gateway","status":502,"detail":"The origin web server returned an invalid or incomplete response to Cloudflare.","error_code":502,"error_name":"origin_bad_gateway","retry_after":60}""",
        "origin_bad_gateway", "The origin web server returned an invalid or incomplete response to Cloudflare.")]
    public async Task ErrorsPreserveBothApiFormats(int status, string body, string? apiCode, string message)
    {
        using var handler = new RecordingHandler { StatusCode = (HttpStatusCode)status, Response = body };
        using var api = CreateApi(handler);
        var result = await api.Users.StatsAsync(42);
        result.IsOk().Should().BeFalse();
        result.Error.Code.Should().Be(status);
        result.Error.ApiCode.Should().Be(apiCode);
        result.Error.Message.Should().Be(message);
        result.Error.RawResponse.Should().Be(body);
        result.TryGet(out _).Should().BeFalse();
        result.Invoking(value => value.Get()).Should().Throw<InvalidOperationException>();
        if (status == 403) result.Error.Field.Should().Be("users");
    }

    [TestMethod]
    public async Task RateLimitExposesRetryAfterFromHeaderOrBody()
    {
        using var handler = new RecordingHandler { StatusCode = (HttpStatusCode)429, RetryAfter = 12,
            Response = """{"code":"too_fast","message":"Slow down","retry_after":5}""" };
        using (var api = CreateApi(handler))
        {
            var error = (await api.Users.StatsAsync(42)).Error;
            error.RetryAfterSeconds.Should().Be(12);
            error.RetryAfter.Should().Be(TimeSpan.FromSeconds(12));
        }
        // A new client: the first one stays paused for 12 s after its 429.
        handler.RetryAfter = null;
        using (var api = CreateApi(handler))
        {
            var error = (await api.Users.StatsAsync(42)).Error;
            error.RetryAfterSeconds.Should().Be(5);
            error.RetryAfter.Should().Be(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    public async Task EmptyCompatibilityEpisodesAndHealthAreSuccessful()
    {
        using var handler = new RecordingHandler { Response = "" };
        using var api = CreateApi(handler);
        var episodes = (await api.Titles.EpisodesAsync(42)).Get();
        episodes.Episodes.Should().BeEmpty();
        episodes.ConnectedEpisodes.Should().BeEmpty();
        (await api.HealthAsync()).Get().Response.Should().BeEmpty();
        (await api.Titles.InfoAsync(42)).Error.Type.Should().Be(ErrorType.Parsing);
    }

    [TestMethod]
    public void ApiLimitsAreValidated()
    {
        Action userLimit = () => new User("nick", 21);
        Action titleLimit = () => new TitleQuick("title", limit: 51);
        Action historyLimit = () => new Queries.User.LastRead(42, 101);
        Action seasonYear = () => new SeasonTitles(1899, Season.Fall);
        Action seasonLimit = () => new SeasonTitles(2026, Season.Fall, limit: 2001);
        Action currentSeasonYear = () => new Queries.Episode.CurrentSeason(2101, Season.Winter);
        Action currentSeasonName = () => new Queries.Episode.CurrentSeason(2026, (Season)7);
        Action emptyIds = () => new TitlesByIds(Array.Empty<ulong>());
        Action tooManyIds = () => new TitlesByIds(Enumerable.Repeat(1UL, 101));
        foreach (var action in new[] { userLimit, titleLimit, historyLimit, seasonYear, seasonLimit, currentSeasonYear, currentSeasonName, emptyIds, tooManyIds })
            action.Should().Throw<ArgumentException>();
    }
}
