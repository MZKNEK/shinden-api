using System.Net.Http;
using Microsoft.Extensions.Logging;
using ShindenAPI.Credentials;

namespace ShindenAPI.Test;

[TestClass]
public class DiagnosticsTests
{
    private const string Token = "private-token&value=with space";

    private static Shinden CreateApi(HttpMessageHandler handler, ILogger<Shinden> logger, TimeSpan? timeout = null)
        => new(config => config.WithUri("https://fake.shinden.test/").WithMaxRetries(0)
            .WithRequestTimeout(timeout ?? TimeSpan.FromSeconds(10))
            .WithAuth(new Auth { Token = Token, UserAgent = "test" }).Build(), logger, handler: handler);

    [TestMethod]
    public async Task ParsingLogsKeepJsonLocationModelAndSanitizedExceptionDetails()
    {
        using var handler = new RecordingHandler { Response = "{\n\"userId\":42,\n\"anime\":{\"episodesWatched\":\"" + Token + "\"}}" };
        var logger = new CaptureLogger();
        using var api = CreateApi(handler, logger);

        var error = (await api.Users.StatsAsync(42)).Error;

        error.Type.Should().Be(ErrorType.Parsing);
        error.ExceptionType.Should().Be("Newtonsoft.Json.JsonSerializationException");
        error.JsonPath.Should().Be("anime.episodesWatched");
        error.JsonLineNumber.Should().Be(3);
        error.JsonLinePosition.Should().BeGreaterThan(0);
        var entry = logger.Failures.Single();
        entry.Level.Should().Be(LogLevel.Error);
        entry.Fields["method"]!.ToString().Should().Be("GET");
        entry.Fields["uri"].Should().Be("/v1/users/42/stats");
        entry.Fields["model"].Should().Be("ShindenAPI.Models.UserStats");
        entry.Fields["exceptionType"].Should().Be(error.ExceptionType);
        entry.Fields["jsonPath"].Should().Be(error.JsonPath);
        entry.Fields["lineNumber"].Should().Be(error.JsonLineNumber);
        entry.Fields["linePosition"].Should().Be(error.JsonLinePosition);
        entry.Exception!.StackTrace.Should().Contain("Newtonsoft.Json");
        entry.Exception.ToString().Should().Contain(error.ExceptionType).And.Contain("anime.episodesWatched");
        AssertSecretsAreAbsent(logger);
    }

    [TestMethod]
    public async Task InvalidJsonSyntaxExposesReaderLocation()
    {
        using var handler = new RecordingHandler { Response = "{\"userId\":42,\n\"anime\":!}" };
        var logger = new CaptureLogger();
        using var api = CreateApi(handler, logger);

        var error = (await api.Users.StatsAsync(42)).Error;

        error.Type.Should().Be(ErrorType.Parsing);
        error.ExceptionType.Should().Be("Newtonsoft.Json.JsonReaderException");
        error.JsonPath.Should().Be("anime");
        error.JsonLineNumber.Should().Be(2);
        error.JsonLinePosition.Should().BeGreaterThan(0);
        logger.Failures.Single().Fields["jsonPath"].Should().Be("anime");
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("null")]
    public async Task EmptyParsedResponsesAreLoggedAsFailures(string response)
    {
        using var handler = new RecordingHandler { Response = response };
        var logger = new CaptureLogger();
        using var api = CreateApi(handler, logger);

        var error = (await api.Users.StatsAsync(42)).Error;

        error.Type.Should().Be(ErrorType.Parsing);
        logger.Failures.Single().Text.Should().Contain("parsing failed").And.Contain("empty response");
    }

    [DataTestMethod]
    [DataRow("stats", "{}", "userId")]
    [DataRow("profile", "{}", "user_id")]
    [DataRow("character", "{}", "character_id")]
    [DataRow("anime-card", "{}", "id")]
    [DataRow("character-card", "{}", "id")]
    [DataRow("title", "{}", "title")]
    [DataRow("title", "{\"title\":{}}", "title_id")]
    [DataRow("stats", "{\"userId\":null}", "userId")]
    [DataRow("users", "[{}]", "user_id")]
    public async Task MissingRequiredIdentityIsReportedInsteadOfReturningDefaultValues(string operation, string response, string field)
    {
        using var handler = new RecordingHandler { Response = response };
        var logger = new CaptureLogger();
        using var api = CreateApi(handler, logger);

        var error = operation switch
        {
            "profile" => (await api.Users.InfoAsync(42)).Error,
            "character" => (await api.Characters.InfoAsync(42)).Error,
            "anime-card" => (await api.Titles.AnimeAsync(42)).Error,
            "character-card" => (await api.Characters.CatalogAsync(42)).Error,
            "title" => (await api.Titles.InfoAsync(42)).Error,
            "users" => (await api.Search.UsersAsync("name")).Error,
            _ => (await api.Users.StatsAsync(42)).Error
        };

        error.Type.Should().Be(ErrorType.Parsing);
        logger.Failures.Single().Text.Should().Contain(field);
    }

    [TestMethod]
    public async Task OptionalFieldsAndNewServerFieldsRemainSupported()
    {
        using var handler = new RecordingHandler { Response = """{"userId":42,"futureServerField":true}""" };
        var logger = new CaptureLogger();
        using var api = CreateApi(handler, logger);

        var result = await api.Users.StatsAsync(42);

        result.Get().UserId.Should().Be(42);
        result.Value.Anime.Should().BeNull();
        logger.Failures.Should().BeEmpty();
    }

    [TestMethod]
    public async Task TimeoutsAreLoggedWithEndpointAndDuration()
    {
        using var handler = new FailureHandler(block: true);
        var logger = new CaptureLogger();
        using var api = CreateApi(handler, logger, TimeSpan.FromMilliseconds(30));

        var result = await api.Users.StatsAsync(42);

        result.Error.Type.Should().Be(ErrorType.Timeout);
        var entry = logger.Failures.Single();
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Text.Should().Contain("request timed out");
        entry.Fields["uri"].Should().Be("/v1/users/42/stats");
        entry.Fields["time"].Should().BeOfType<long>().Which.Should().BeGreaterThanOrEqualTo(0);
        entry.Fields["exceptionType"].Should().Be(result.Error.ExceptionType);
    }

    [TestMethod]
    public async Task CallerCancellationIsNotLoggedAsTimeoutOrFailure()
    {
        using var handler = new FailureHandler(block: true);
        var logger = new CaptureLogger();
        using var api = CreateApi(handler, logger);
        using var cancellation = new CancellationTokenSource();
        var pending = api.Users.StatsAsync(42, cancellation.Token);
        await handler.Started.Task;
        cancellation.Cancel();

        await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () => await pending);

        logger.Failures.Should().BeEmpty();
    }

    [TestMethod]
    public async Task TransportLogsPreserveInnerStackAndTypeWithoutExceptionDataOrSecrets()
    {
        using var handler = new FailureHandler(block: false);
        var logger = new CaptureLogger();
        using var api = CreateApi(handler, logger);

        var result = await api.Users.StatsAsync(42);

        result.Error.Type.Should().Be(ErrorType.Exception);
        var entry = logger.Failures.Single();
        entry.Fields["exceptionType"].Should().Be(typeof(HttpRequestException).FullName);
        entry.Exception!.StackTrace.Should().Contain(nameof(FailureHandler));
        entry.Exception.InnerException!.StackTrace.Should().Contain(nameof(FailureHandler));
        entry.Exception.InnerException.Message.Should().Contain("***");
        entry.Exception.Data.Count.Should().Be(0);
        entry.Exception.ToString().Should().Contain(typeof(HttpRequestException).FullName).And.Contain(typeof(InvalidOperationException).FullName);
        AssertSecretsAreAbsent(logger);
    }

    private static void AssertSecretsAreAbsent(CaptureLogger logger)
    {
        foreach (var entry in logger.Entries)
        {
            var diagnostics = entry.Text + entry.Exception + string.Join(" ", entry.Fields.Values);
            diagnostics.Should().NotContain(Token).And.NotContain(Uri.EscapeDataString(Token));
            for (var exception = entry.Exception; exception is not null; exception = exception.InnerException)
            {
                (exception.Message + exception.StackTrace).Should().NotContain(Token);
                exception.Data.Count.Should().Be(0);
            }
        }
    }

    private sealed class FailureHandler : HttpMessageHandler
    {
        private readonly bool _block;
        internal TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal FailureHandler(bool block) => _block = block;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.TrySetResult(true);
            if (_block) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            try { throw new InvalidOperationException("Inner failure: " + Token); }
            catch (Exception inner)
            {
                var error = new HttpRequestException("Outer failure: " + Uri.EscapeDataString(Token), inner);
                error.Data["private"] = Token;
                throw error;
            }
        }
    }

    private sealed record Entry(LogLevel Level, string Text, Exception? Exception, Dictionary<string, object?> Fields);

    private sealed class CaptureLogger : ILogger<Shinden>
    {
        internal List<Entry> Entries { get; } = new();
        internal IEnumerable<Entry> Failures => Entries.Where(entry => entry.Level >= LogLevel.Warning);
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add(new Entry(level, formatter(state, exception), exception,
                ((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary(pair => pair.Key, pair => pair.Value)));
    }
}
