using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using ShindenAPI.LiveTests;
using LiveProgram = ShindenAPI.LiveTests.Program;

namespace ShindenAPI.Test;

[TestClass]
public class LiveRunnerTests
{
    private static LiveSettings Settings(bool login = false) => new()
    {
        NewApi = new() { BaseUri = "https://live.fake.test/", Authentication = new() { BearerToken = "newsecret" } },
        LegacyApi = new()
        {
            BaseUri = "https://live.fake.test/", Authentication = new() { ApiKey = "oldsecret" },
            UserAuthentication = new() { Enabled = login, Username = "login-name", Password = "passwordsecret" }
        },
        IncludeBulkCharacterLists = true,
        IncludePocketWaifu = true
    };

    [TestMethod]
    public void RunningLiveRequiresExplicitCommandAndCiIsRejected()
    {
        Action noMode = () => LiveProgram.ParseArguments(new[] { "--config", "secret.local.json" });
        Action bothModes = () => LiveProgram.ParseArguments(new[] { "--run-live", "--validate" });
        noMode.Should().Throw<SettingsException>();
        bothModes.Should().Throw<SettingsException>();
        LiveProgram.ParseArguments(new[] { "--run-live" }).Run.Should().BeTrue();
        LiveProgram.ParseArguments(new[] { "--validate" }).Run.Should().BeFalse();
        LiveProgram.ParseArguments(new[] { "--run-live", "--only", "compat.current-season" }).Only.Should().Be("compat.current-season");
        ((Action)(() => LiveProgram.ParseArguments(new[] { "--run-live", "--only" }))).Should().Throw<SettingsException>();
        LiveProgram.IsCi(name => name == "CI" ? "true" : null).Should().BeTrue();
        LiveProgram.IsCi(name => name == "GITHUB_ACTIONS" ? "true" : null).Should().BeTrue();
        LiveProgram.IsCi(name => name == "CI" ? "false" : null).Should().BeFalse();
    }

    [TestMethod]
    public async Task OnlyFilterSendsRequestsForMatchingChecksOnly()
    {
        var settings = Settings(login: true);
        using var handler = new LiveFakeHandler();
        var runner = new LiveRunner(settings, TextWriter.Null, handler, _ => Task.CompletedTask, only: "compat.current-season");
        await runner.RunAsync(CancellationToken.None);
        runner.Report.Failed.Should().Be(0, JsonConvert.SerializeObject(runner.Report));
        runner.Report.Passed.Should().Be(2);
        handler.Requests.Should().HaveCount(2).And.OnlyContain(request => request.Path == "/compat/php-api/anime/current-season");
    }

    [TestMethod]
    public void ConfigurationRequiresSeparateKeysAndExplicitWrites()
    {
        var settings = Settings(login: true);
        settings.Validate(false);
        settings.NewApi.Authentication.BearerToken = "";
        settings.Invoking(value => value.Validate(false)).Should().Throw<SettingsException>();
        settings.NewApi.AuthenticatedTestsEnabled = false;
        settings.Validate(false);
        settings.Writes.Enabled = true;
        settings.Invoking(value => value.Validate(false)).Should().Throw<SettingsException>();
        settings.Invoking(value => value.Validate(true)).Should().Throw<SettingsException>();
        settings.Writes.ExpectedUserId = 42;
        settings.Writes.Favourite = new() { Enabled = true, ObjectId = 5, InitiallyFavourite = false };
        settings.Validate(true);
    }

    [TestMethod]
    public async Task AllReadFamiliesRunOfflineWithoutMutationsAndKeepKeysSeparate()
    {
        var settings = Settings(login: true);
        settings.Validate(false);
        using var handler = new LiveFakeHandler();
        var runner = new LiveRunner(settings, TextWriter.Null, handler, _ => Task.CompletedTask);
        await runner.RunAsync(CancellationToken.None);
        runner.Report.Failed.Should().Be(0, JsonConvert.SerializeObject(runner.Report));
        runner.Report.Passed.Should().BeGreaterThan(35);
        handler.Requests.Should().OnlyContain(request => request.Method == "GET" || request.Path == "/api/user/login");
        handler.Requests.Where(request => request.Path.StartsWith("/api/")).Should().OnlyContain(request => request.Authorization == null && request.Query.Contains("api_key=oldsecret"));
        handler.Requests.Where(request => !request.Path.StartsWith("/api/")).Should().OnlyContain(request => request.Cookie == "proba=1" && !request.Query.Contains("api_key"));
        handler.Requests.Should().Contain(request => request.Authorization == "Bearer newsecret");
        handler.Requests.Should().Contain(request => request.Path == "/api/user/42/fav-chars" && request.Cookie == "name=session-name; id=session-id; proba=1");
        handler.Requests.Single(request => request.Path == "/api/user/login").Cookie.Should().Be("proba=1");
        foreach (var request in handler.Requests)
        {
            request.Cookie!.Split(';').Select(value => value.Trim()).Should().ContainSingle(value => value == "proba=1");
            request.Accept.Should().Be("application/json");
        }
    }

    [TestMethod]
    public async Task GuestOnlyConfigurationDoesNotSendDisabledCredentials()
    {
        var settings = Settings();
        settings.NewApi.AuthenticatedTestsEnabled = false;
        settings.LegacyApi.Enabled = false;
        settings.Validate(false);
        using var handler = new LiveFakeHandler();
        var runner = new LiveRunner(settings, TextWriter.Null, handler, _ => Task.CompletedTask);
        await runner.RunAsync(CancellationToken.None);
        runner.Report.Failed.Should().Be(0);
        handler.Requests.Should().OnlyContain(request => request.Authorization == null && !request.Path.StartsWith("/api/"));
        runner.Report.Checks.Should().Contain(check => check.Name == "compat.users.info" && check.Status == "SKIP");
    }

    [TestMethod]
    public async Task ReportsAndConsoleDoNotContainReflectedAuthenticationValues()
    {
        var settings = Settings();
        using var handler = new LiveFakeHandler { LeakStatsError = true };
        using var output = new StringWriter();
        var runner = new LiveRunner(settings, output, handler, _ => Task.CompletedTask);
        await runner.RunAsync(CancellationToken.None);
        runner.Report.Failed.Should().Be(1);
        var text = JsonConvert.SerializeObject(runner.Report) + output;
        foreach (var secret in new[] { "newsecret", "oldsecret", "passwordsecret", "session-id", "session-name" })
            text.Should().NotContain(secret);
        LiveRunner.ParsingPath("Could not convert value 'passwordsecret'. Path 'items[0].avatar', line 1.").Should().Be("items[0].avatar");
    }

    [TestMethod]
    public async Task FailedMutationStillAttemptsRestorationWithoutRetry()
    {
        var settings = WriteSettings();
        using var handler = new LiveFakeHandler { FailFavouriteWrite = true };
        var runner = new LiveRunner(settings, TextWriter.Null, handler, _ => Task.CompletedTask);
        await runner.RunAsync(CancellationToken.None);
        handler.Requests.Count(request => request.Path == "/api/userlist/42/fav" && request.Method == "POST").Should().Be(1);
        handler.Requests.Count(request => request.Path == "/api/userlist/42/fav" && request.Method == "DELETE").Should().Be(1);
        runner.Report.Checks.Should().Contain(check => check.Name == "cleanup.favourite" && check.Status == "PASS");
        runner.Report.Failed.Should().Be(1);
    }

    [TestMethod]
    public async Task WrongLoginIdentityPreventsAllWrites()
    {
        var settings = WriteSettings();
        settings.Writes.ExpectedUserId = 99;
        using var handler = new LiveFakeHandler();
        var runner = new LiveRunner(settings, TextWriter.Null, handler, _ => Task.CompletedTask);
        await runner.RunAsync(CancellationToken.None);
        handler.Requests.Should().NotContain(request => request.Path.StartsWith("/api/userlist/"));
        runner.Report.Checks.Should().Contain(check => check.Name == "writes.identity" && check.Status == "FAIL");
    }

    [TestMethod]
    public async Task FailedRestorationIsReportedWithRecoveryInstructions()
    {
        var settings = WriteSettings();
        using var handler = new LiveFakeHandler { FailFavouriteCleanup = true };
        var runner = new LiveRunner(settings, TextWriter.Null, handler, _ => Task.CompletedTask);
        await runner.RunAsync(CancellationToken.None);
        var cleanup = runner.Report.Checks.Single(check => check.Name == "cleanup.favourite");
        cleanup.Status.Should().Be("FAIL");
        cleanup.Details.Should().Contain("Manual restoration verification").And.Contain("character/5");
    }

    [TestMethod]
    public async Task CancelledWriteAttemptsCleanupWithAnIndependentToken()
    {
        var settings = WriteSettings();
        using var cancellation = new CancellationTokenSource();
        using var handler = new LiveFakeHandler { CancelFavouriteWrite = cancellation };
        var runner = new LiveRunner(settings, TextWriter.Null, handler, _ => Task.CompletedTask);
        Func<Task> run = () => runner.RunAsync(cancellation.Token);
        await run.Should().ThrowAsync<OperationCanceledException>();
        handler.Requests.Should().Contain(request => request.Method == "DELETE" && request.Path == "/api/userlist/42/fav");
        runner.Report.Cancelled.Should().BeTrue();
        runner.Report.Checks.Should().Contain(check => check.Name == "cleanup.favourite" && check.Status == "PASS");
    }

    private static LiveSettings WriteSettings()
    {
        var settings = Settings(login: true);
        settings.Writes.Enabled = true;
        settings.Writes.ExpectedUserId = 42;
        settings.Writes.Favourite = new() { Enabled = true, Type = "character", ObjectId = 5, InitiallyFavourite = false };
        settings.Validate(true);
        return settings;
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FavouriteReadsVerifyAdditionAndRemovalAndRestoreTheOriginalList(bool initiallyFavourite)
    {
        var settings = WriteSettings();
        settings.Writes.Favourite.InitiallyFavourite = initiallyFavourite;
        using var handler = new LiveFakeHandler();
        handler.FavouriteIds.Add(99);
        if (initiallyFavourite) handler.FavouriteIds.Add(5);
        var runner = new LiveRunner(settings, TextWriter.Null, handler, _ => Task.CompletedTask);

        await runner.RunAsync(CancellationToken.None);

        runner.Report.Failed.Should().Be(0, JsonConvert.SerializeObject(runner.Report));
        foreach (var name in new[] { "php.users.favourite-characters", "php.session.read-favourites" })
        {
            runner.Report.Checks.Single(check => check.Name == name).Status.Should().Be("PASS");
            runner.Report.Checks.Single(check => check.Name == name + ".after-remove").Status.Should().Be("PASS");
            if (initiallyFavourite)
                runner.Report.Checks.Single(check => check.Name == name + ".restored").Status.Should().Be("PASS");
        }
        handler.FavouriteIds.Should().BeEquivalentTo(initiallyFavourite ? new ulong[] { 5, 99 } : new ulong[] { 99 });
        var requests = handler.Requests.Where(request => request.Path is "/api/userlist/42/fav" or "/api/user/42/fav-chars").ToArray();
        requests.Select(request => request.Method).Should().Equal(initiallyFavourite
            ? new[] { "GET", "DELETE", "POST", "GET", "GET", "DELETE", "GET", "GET", "POST", "GET", "GET" }
            : new[] { "GET", "POST", "GET", "GET", "DELETE", "GET", "GET" });
        var addIndex = Array.FindIndex(requests, request => request.Method == "POST");
        requests[addIndex + 1].Cookie.Should().Be("proba=1");
        requests[addIndex + 2].Cookie.Should().Contain("id=session-id");
    }

    [TestMethod]
    public async Task FavouriteReadsAcceptEmpty404OnlyBeforeAddingAndAfterRemoval()
    {
        using var handler = new LiveFakeHandler { Favourite404Body = "" };
        var runner = new LiveRunner(WriteSettings(), TextWriter.Null, handler, _ => Task.CompletedTask);

        await runner.RunAsync(CancellationToken.None);

        runner.Report.Failed.Should().Be(0, JsonConvert.SerializeObject(runner.Report));
        runner.Report.Checks.Where(check => check.Name.Contains("favourite")).Should().OnlyContain(check => check.Status == "PASS");
        handler.FavouriteIds.Should().BeEmpty();
    }

    [TestMethod]
    public async Task SuccessfulFavouriteWriteMustActuallyPopulateBothReadResults()
    {
        using var handler = new LiveFakeHandler { IgnoreFavouriteAddition = true, Favourite404Body = "" };
        var runner = new LiveRunner(WriteSettings(), TextWriter.Null, handler, _ => Task.CompletedTask);

        await runner.RunAsync(CancellationToken.None);

        runner.Report.Checks.Single(check => check.Name == "write.favourite").Status.Should().Be("PASS");
        foreach (var name in new[] { "php.users.favourite-characters", "php.session.read-favourites" })
        {
            runner.Report.Checks.Single(check => check.Name == name).Status.Should().Be("FAIL");
            runner.Report.Checks.Single(check => check.Name == name + ".after-remove").Status.Should().Be("PASS");
        }
        runner.Report.Failed.Should().Be(2);
        handler.FavouriteIds.Should().BeEmpty();
    }

    [TestMethod]
    public async Task IncorrectInitialFavouriteStatePreventsFavouriteWrites()
    {
        using var handler = new LiveFakeHandler();
        handler.FavouriteIds.Add(5);
        var runner = new LiveRunner(WriteSettings(), TextWriter.Null, handler, _ => Task.CompletedTask);

        await runner.RunAsync(CancellationToken.None);

        runner.Report.Checks.Single(check => check.Name == "write.favourite.initial-state").Status.Should().Be("FAIL");
        handler.Requests.Should().NotContain(request => request.Path == "/api/userlist/42/fav");
        handler.FavouriteIds.Should().Equal(5);
    }

    [TestMethod]
    public async Task AllWriteScenariosRestoreConfiguredValues()
    {
        var settings = WriteSettings();
        settings.Writes.TitleStatus = new() { Enabled = true, TitleId = 11, InitialStatus = "plan", InitialStateConfirmed = true };
        settings.Writes.AnimeRating = new() { Enabled = true, TitleId = 11, TestValue = 8, RestoreValue = 7 };
        settings.Writes.MangaRating = new() { Enabled = true, TitleId = 12, TestValue = 9, RestoreValue = 6 };
        settings.Writes.IncreaseWatched = new() { Enabled = true, TitleId = 11 };
        settings.Validate(true);
        using var handler = new LiveFakeHandler();
        var runner = new LiveRunner(settings, TextWriter.Null, handler, _ => Task.CompletedTask);
        await runner.RunAsync(CancellationToken.None);
        runner.Report.Failed.Should().Be(0, JsonConvert.SerializeObject(runner.Report));
        handler.Requests.Last(request => request.Path == "/api/userlist/42/series/11").Body.Should().Be("status=plan");
        handler.Requests.Last(request => request.Path == "/api/anime/11/rate").Body.Should().Be("type=total&value=7");
        handler.Requests.Last(request => request.Path == "/api/manga/12/rate").Body.Should().Be("type=total&value=6");
        handler.Requests.Count(request => request.Path == "/api/userlist/42/increase-watched/11").Should().Be(1);
        foreach (var request in handler.Requests)
        {
            request.Cookie!.Split(';').Select(value => value.Trim()).Should().ContainSingle(value => value == "proba=1");
            request.Accept.Should().Be("application/json");
        }
    }

    [TestMethod]
    public async Task LiveCookieIsMergedWithoutDuplicatingItOrDisposingTheProvidedHandler()
    {
        using var handler = new LiveFakeHandler();
        using (var transport = new LiveCookieHandler(handler))
        using (var client = new HttpClient(transport, disposeHandler: false))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://live.fake.test/health");
            request.Headers.TryAddWithoutValidation("Cookie", "name=session-name; id=session-id; proba=0; other=keep");
            using var response = await client.SendAsync(request);
            handler.Requests.Last().Cookie.Should().Be("name=session-name; id=session-id; other=keep; proba=1");

            using var alreadySet = new HttpRequestMessage(HttpMethod.Get, "https://live.fake.test/health");
            alreadySet.Headers.TryAddWithoutValidation("Cookie", "proba=1");
            using var second = await client.SendAsync(alreadySet);
            handler.Requests.Last().Cookie.Should().Be("proba=1");
        }
        handler.WasDisposed.Should().BeFalse();
    }

    [TestMethod]
    public async Task AbsentInitialListEntryIsNotDeletedTwice()
    {
        var settings = WriteSettings();
        settings.Writes.TitleStatus = new() { Enabled = true, TitleId = 11, InitialStateConfirmed = true };
        using var handler = new LiveFakeHandler();
        var runner = new LiveRunner(settings, TextWriter.Null, handler, _ => Task.CompletedTask);
        await runner.RunAsync(CancellationToken.None);
        runner.Report.Failed.Should().Be(0);
        handler.Requests.Count(request => request.Path == "/api/userlist/42/series/11" && request.Method == "DELETE").Should().Be(1);
    }

    [DataTestMethod]
    [DataRow("", "SKIP")]
    [DataRow("<html>Missing route</html>", "FAIL")]
    public async Task OnlyEmptyFavourite404ResponsesAreTreatedAsMissingData(string body, string expected)
    {
        var settings = Settings(login: true);
        using var handler = new LiveFakeHandler { Favourite404Body = body };
        var runner = new LiveRunner(settings, TextWriter.Null, handler, _ => Task.CompletedTask);
        await runner.RunAsync(CancellationToken.None);
        runner.Report.Checks.Single(check => check.Name == "php.users.favourite-characters").Status.Should().Be(expected);
        runner.Report.Checks.Single(check => check.Name == "php.session.read-favourites").Status.Should().Be(expected);
    }

    [TestMethod]
    public async Task ValidationModeReadsConfigurationWithoutRunningTests()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(Settings(), LiveSettings.JsonSettings));
            (await LiveProgram.RunAsync(new[] { "--validate", "--config", path })).Should().Be(0);
            await File.WriteAllTextAsync(path, """{"newApi":{"authentication":{"bearerToken":{"unexpected":"private-value"}}}}""");
            Action load = () => LiveSettings.Load(path);
            load.Should().Throw<SettingsException>().Which.Message.Should().NotContain("private-value");
        }
        finally { File.Delete(path); }
    }

    private sealed record RequestInfo(string Path, string Query, string Method, string? Authorization, string? Cookie, string? Body, string Accept);
    private sealed class LiveFakeHandler : HttpMessageHandler
    {
        internal List<RequestInfo> Requests { get; } = new();
        internal bool LeakStatsError { get; set; }
        internal bool FailFavouriteWrite { get; set; }
        internal bool FailFavouriteCleanup { get; set; }
        internal bool IgnoreFavouriteAddition { get; set; }
        internal HashSet<ulong> FavouriteIds { get; } = new();
        internal string? Favourite404Body { get; set; }
        internal CancellationTokenSource? CancelFavouriteWrite { get; set; }
        internal bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(new RequestInfo(path, request.RequestUri.Query, request.Method.Method, request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("Cookie", out var cookies) ? string.Join("; ", cookies) : null,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
                string.Join(", ", request.Headers.Accept.Select(value => value.ToString()))));
            if (path == "/v1/users/42/stats" && LeakStatsError)
                return await Json("""{"code":"newsecret","message":"newsecret oldsecret passwordsecret session-id session-name"}""", HttpStatusCode.Unauthorized);
            if (path == "/api/user/42/fav-chars")
            {
                if (FavouriteIds.Count == 0 && Favourite404Body is not null)
                    return await Json(Favourite404Body, HttpStatusCode.NotFound);
                return await Json(JsonConvert.SerializeObject(FavouriteIds.Select(id => new { character_id = id, first_name = "Character" })));
            }
            if (path == "/api/userlist/42/fav")
            {
                var fail = request.Method == HttpMethod.Post ? FailFavouriteWrite : FailFavouriteCleanup;
                if (fail) return await Json("{}", HttpStatusCode.ServiceUnavailable);
                var characterId = ulong.Parse(Requests.Last().Body!.Split('-').Last());
                var changed = request.Method == HttpMethod.Post
                    ? IgnoreFavouriteAddition || FavouriteIds.Add(characterId) : FavouriteIds.Remove(characterId);
                if (request.Method == HttpMethod.Post) CancelFavouriteWrite?.Cancel();
                return await Json(JsonConvert.SerializeObject(new { updated = changed ? 1 : 0 }));
            }
            if (path == "/api/userlist/42/series/11")
            {
                if (request.Method == HttpMethod.Delete) return await Json("""{"status":"ok"}""");
                var status = Uri.UnescapeDataString(Requests.Last().Body!.Split('=', 2)[1]).Replace('+', ' ');
                return await Json(JsonConvert.SerializeObject(new { status = "ok", series_status = new { watch_status = status } }));
            }
            var body = path switch
            {
                "/health" => "",
                "/v1/search/users" => """[{"user_id":42,"name":"user","avatar":true}]""",
                "/v1/search/titles" => request.RequestUri.Query.Contains("types=Manga")
                    ? """[{"title_id":12,"title":"Manga","type":"Manga"}]""" : """[{"title_id":11,"title":"Anime","type":"Anime"}]""",
                "/v1/search/characters" => """[{"character_id":5,"first_name":"Name"}]""",
                "/v1/search/staff" => """[{"staff_id":6,"first_name":"Person"}]""",
                "/v1/search/tags" => """[{"tag_id":1,"tag_name":"Tag"}]""",
                "/v1/search/titles/by-ids" => """{"items":[{"id":11,"title":"Anime"},{"id":12,"title":"Manga"}]}""",
                "/v1/search/browse/anime" or "/v1/search/browse/manga" or "/v1/search/season/2026/fall" => """{"total":1,"items":[{"title_id":11}]}""",
                "/v1/catalog/anime/11" => """{"id":11,"title":"Anime"}""",
                "/v1/catalog/characters/5" => """{"id":5,"firstName":"Name"}""",
                "/v1/users/42/stats" => """{"userId":42,"anime":{},"manga":{}}""",
                "/compat/php-api/user/42/info" => """{"user_id":"42","name":"user"}""",
                "/compat/php-api/user/42/last_view" or "/compat/php-api/user/42/last_read" or "/api/user/42/fav-chars" => "[]",
                "/compat/php-api/character/5" => """{"character_id":"5","first_name":"Name"}""",
                "/compat/php-api/title/11/info" => """{"title":{"title_id":"11","title":"Anime"}}""",
                "/compat/php-api/title/11/episodes" => """{"episodes":[],"connected_episodes":[]}""",
                "/compat/php-api/title/11/characters" => """{"relations":[]}""",
                "/compat/php-api/episode/new" => """{"lastonline":[]}""",
                "/compat/php-api/anime/current-season" => request.RequestUri.Query.Contains("season=summer")
                    ? """{"season":{"year":2026,"season":"summer"},"updated_at":"2026-11-07 12:00:00","titles":[10],"characters":["4"],"character_titles":[{"character_id":4,"title_id":10}]}"""
                    : """{"season":{"year":2026,"season":"fall"},"updated_at":"2026-11-07 12:00:00","titles":[11],"characters":["5"],"character_titles":[{"character_id":5,"title_id":11}]}""",
                "/compat/php-api/character/flat-list" or "/compat/php-api/character/flat-list-anime" or "/api/character/flat-list-book" => """[{"c":5,"f":"Name","n":1}]""",
                "/api/episode/11/range" => """{"min_no":"1","max_no":"12"}""",
                "/api/title/11/recommendations" => """{"recommendations":[]}""",
                "/api/title/11/reviews" => """{"reviews":[]}""",
                "/api/title/11/related" => """{"related_titles":[]}""",
                "/api/staff/6" => """{"staff_id":6}""",
                "/api/character/connected" or "/api/character/in-anime" or "/api/character/in-manga" => "[5]",
                "/v1/pw/genres" => """{"items":[],"dere":[]}""",
                "/v1/users/42/pw/cards" => """{"status":"ok","items":[{"id":7}],"nextCursor":null}""",
                "/v1/users/42/pw/cards/ids" => """{"ids":[7],"total":1,"truncated":false}""",
                "/v1/users/42/pw/tags" => """{"status":"ok","items":[]}""",
                "/v1/pw/cards/7" => """{"status":"ok","card":{"id":7}}""",
                "/api/user/login" => """{"logged_user":{"user_id":42,"name":"user","avatar":"18"},"session":{"id":"session-id","name":"session-name"}}""",
                "/api/anime/11/rate" or "/api/manga/12/rate" => """{"status":"ok"}""",
                "/api/userlist/42/increase-watched/11" => """{"status":"ok","episode_added":"1"}""",
                _ => null
            };
            return body is null ? await Json("{}", HttpStatusCode.NotFound) : await Json(body);
        }
        private static Task<HttpResponseMessage> Json(string content, HttpStatusCode code = HttpStatusCode.OK)
            => Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(content) });
    }
}
