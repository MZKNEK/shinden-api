using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using ShindenAPI.Credentials;

namespace ShindenAPI.Test;

[TestClass]
public class RateLimitTests
{
    private const string Users = """[{"user_id":85,"name":"nick","rank":"Tester","avatar":true}]""";
    private const string TooFast = """{"code":"too_fast","message":"Slow down"}""";

    private static Shinden CreateApi(HttpMessageHandler handler, Func<ConfigBuilder, ConfigBuilder>? configure = null,
        ILogger<Shinden>? logger = null) => new(config => (configure ?? (builder => builder))(config
            .WithUri("https://new.shinden.test/")
            .WithAuth(new Auth { Token = "shk_new", UserAgent = "new-agent" })
            .WithLegacyUri("https://php.shinden.test/")
            .WithLegacyAuth(new Auth { Token = "old_key", UserAgent = "php-agent" })
            .WithMaxRetries(0).WithRetryDelay(TimeSpan.Zero)).Build(), logger!, handler: handler);

    [TestMethod]
    public async Task TooManyRequestsPausesTheClientUntilRetryAfter()
    {
        using var handler = new SequenceHandler(Reply.Fail(429, TooFast, TimeSpan.FromSeconds(60)), Reply.Ok(Users));
        using var api = CreateApi(handler);

        var first = await api.Search.UsersAsync("nick");
        first.Error.Code.Should().Be(429);
        first.Error.Type.Should().Be(ErrorType.Http);
        first.Error.ApiCode.Should().Be("too_fast");
        first.Error.RetryAfter.Should().Be(TimeSpan.FromSeconds(60));

        var second = await api.Search.UsersAsync("nick");
        handler.CallCount.Should().Be(1);
        second.Error.Code.Should().Be(429);
        second.Error.Type.Should().Be(ErrorType.RateLimit);
        second.Error.RetryAfter.Should().BeGreaterThan(TimeSpan.FromSeconds(55)).And.BeLessOrEqualTo(TimeSpan.FromSeconds(60));
        second.Error.RetryAfterSeconds.Should().BeInRange(56, 60);
    }

    [TestMethod]
    public async Task VeryLongRetryAfterPausesForAtMostTenMinutes()
    {
        var logger = new CaptureLogger();
        using var handler = new SequenceHandler(Reply.Fail(429, TooFast, TimeSpan.FromDays(3)), Reply.Ok(Users));
        using var api = CreateApi(handler, logger: logger);

        // The server's error still carries what the server asked for.
        (await api.Search.UsersAsync("nick")).Error.RetryAfter.Should().Be(TimeSpan.FromDays(3));
        var refused = (await api.Search.UsersAsync("nick")).Error;
        refused.Type.Should().Be(ErrorType.RateLimit);
        refused.RetryAfter.Should().BeGreaterThan(TimeSpan.FromMinutes(9)).And.BeLessOrEqualTo(TimeSpan.FromMinutes(10));
        logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning
            && entry.Text.Contains("Retry-After 259200 s") && entry.Text.Contains("maximum 600 s"));
    }

    [TestMethod]
    public async Task RetryAfterAsHttpDateIsSupported()
    {
        using var handler = new SequenceHandler(
            Reply.Fail(429, TooFast, date: DateTimeOffset.UtcNow.AddSeconds(30)), Reply.Ok(Users));
        using var api = CreateApi(handler);

        var first = await api.Search.UsersAsync("nick");
        // An HTTP date has whole-second precision.
        first.Error.RetryAfter.Should().BeCloseTo(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2));
        (await api.Search.UsersAsync("nick")).Error.Type.Should().Be(ErrorType.RateLimit);
        handler.CallCount.Should().Be(1);
    }

    [DataTestMethod]
    [DataRow(467)]
    [DataRow(503)]
    public async Task OtherStatusesDoNotPauseTheClient(int status)
    {
        using var handler = new SequenceHandler(Reply.Fail(status, "{}", TimeSpan.FromSeconds(60)), Reply.Ok(Users));
        using var api = CreateApi(handler);

        var first = await api.Search.UsersAsync("nick");
        first.Error.Code.Should().Be(status);
        first.Error.RetryAfter.Should().Be(TimeSpan.FromSeconds(60));
        (await api.Search.UsersAsync("nick")).IsOk().Should().BeTrue();
        handler.CallCount.Should().Be(2);
    }

    [TestMethod]
    public async Task TooManyRequestsWithoutRetryAfterDoesNotPause()
    {
        using var handler = new SequenceHandler(Reply.Fail(429, TooFast), Reply.Ok(Users));
        using var api = CreateApi(handler);

        (await api.Search.UsersAsync("nick")).Error.RetryAfter.Should().BeNull();
        (await api.Search.UsersAsync("nick")).IsOk().Should().BeTrue();
    }

    [TestMethod]
    public async Task GetRequestsAreNotRetriedAfterTooManyRequestsByDefault()
    {
        using var handler = new SequenceHandler(Reply.Fail(429, TooFast, TimeSpan.FromSeconds(1)), Reply.Ok(Users));
        using var api = CreateApi(handler, builder => builder.WithMaxRetries(2));

        (await api.Search.UsersAsync("nick")).Error.Code.Should().Be(429);
        handler.CallCount.Should().Be(1);
    }

    [TestMethod]
    public async Task WaitModeSendsTheRequestWhenThePauseEnds()
    {
        using var handler = new SequenceHandler(Reply.Fail(429, TooFast, TimeSpan.FromSeconds(1)), Reply.Ok(Users));
        using var api = CreateApi(handler, builder => builder.WithRateLimitMaxWait(TimeSpan.FromSeconds(5)));

        (await api.Search.UsersAsync("nick")).Error.Code.Should().Be(429);
        var watch = Stopwatch.StartNew();
        (await api.Search.UsersAsync("nick")).IsOk().Should().BeTrue();
        watch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(800));
        handler.CallCount.Should().Be(2);
    }

    [TestMethod]
    public async Task WaitModeFailsAtOnceWhenThePauseIsLongerThanTheMaximumWait()
    {
        using var handler = new SequenceHandler(Reply.Fail(429, TooFast, TimeSpan.FromSeconds(60)), Reply.Ok(Users));
        using var api = CreateApi(handler, builder => builder.WithRateLimitMaxWait(TimeSpan.FromSeconds(5)));

        await api.Search.UsersAsync("nick");
        var watch = Stopwatch.StartNew();
        (await api.Search.UsersAsync("nick")).Error.Type.Should().Be(ErrorType.RateLimit);
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
        handler.CallCount.Should().Be(1);
    }

    [TestMethod]
    public async Task WaitingForThePauseCanBeCancelled()
    {
        using var handler = new SequenceHandler(Reply.Fail(429, TooFast, TimeSpan.FromSeconds(60)), Reply.Ok(Users));
        using var api = CreateApi(handler, builder => builder.WithRateLimitMaxWait(TimeSpan.FromMinutes(2)));
        await api.Search.UsersAsync("nick");

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await api.Invoking(value => value.Search.UsersAsync("nick", cancellationToken: cancellation.Token))
            .Should().ThrowAsync<OperationCanceledException>();
        handler.CallCount.Should().Be(1);
    }

    [TestMethod]
    public async Task ConfiguredGetRetryWaitsForThePauseAndSendsAgain()
    {
        using var handler = new SequenceHandler(Reply.Fail(429, TooFast, TimeSpan.FromSeconds(1)), Reply.Ok(Users));
        using var api = CreateApi(handler, builder => builder.WithRateLimitRetries(1));

        var watch = Stopwatch.StartNew();
        (await api.Search.UsersAsync("nick")).IsOk().Should().BeTrue();
        watch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(800));
        handler.CallCount.Should().Be(2);
    }

    [TestMethod]
    public async Task ConfiguredGetRetryReturnsTheServerErrorWhenThePauseIsTooLong()
    {
        using var handler = new SequenceHandler(Reply.Fail(429, TooFast, TimeSpan.FromSeconds(60)), Reply.Ok(Users));
        using var api = CreateApi(handler, builder => builder.WithRateLimitRetries(1)
            .WithRateLimitMaxWait(TimeSpan.FromSeconds(5)));

        var result = await api.Search.UsersAsync("nick");
        result.Error.Type.Should().Be(ErrorType.Http);
        result.Error.ApiCode.Should().Be("too_fast");
        handler.CallCount.Should().Be(1);
    }

    [TestMethod]
    public async Task RequestsThatChangeDataAreNeverRetriedAfterTooManyRequests()
    {
        using var handler = new SequenceHandler(Reply.Fail(429, TooFast, TimeSpan.Zero), Reply.Ok("{}"));
        using var api = CreateApi(handler, builder => builder.WithRateLimitRetries(3)
            .WithRateLimitMaxWait(TimeSpan.FromSeconds(5)));

        (await api.Users.RemoveTitleStatusAsync(1, 2)).Error.Code.Should().Be(429);
        handler.CallCount.Should().Be(1);
        handler.Methods.Should().Equal(HttpMethod.Delete);
    }

    [TestMethod]
    public async Task EachApiIsPausedSeparately()
    {
        using var handler = new SequenceHandler(Reply.Fail(429, TooFast, TimeSpan.FromSeconds(60)), Reply.Ok("{}"));
        using var api = CreateApi(handler);

        await api.Search.UsersAsync("nick");
        (await api.Search.UsersAsync("nick")).Error.Type.Should().Be(ErrorType.RateLimit);
        // The PHP API uses separate credentials and is not paused by the new API's 429.
        await api.Users.RemoveTitleStatusAsync(1, 2);
        handler.CallCount.Should().Be(2);
        handler.Hosts.Should().Equal("new.shinden.test", "php.shinden.test");
    }

    [TestMethod]
    public async Task ChangingTheTokenEndsThePauseButOtherChangesKeepIt()
    {
        using var handler = new SequenceHandler(Reply.Fail(429, TooFast, TimeSpan.FromSeconds(60)), Reply.Ok(Users));
        using var api = CreateApi(handler);
        await api.Search.UsersAsync("nick");

        api.Reconfigure(builder => builder.WithMaxRetries(1).Build());
        (await api.Search.UsersAsync("nick")).Error.Type.Should().Be(ErrorType.RateLimit);

        api.Reconfigure(builder => builder.WithAuth(new Auth { Token = "shk_other", UserAgent = "new-agent" }).Build());
        (await api.Search.UsersAsync("nick")).IsOk().Should().BeTrue();
        handler.CallCount.Should().Be(2);
    }

    [TestMethod]
    public async Task PauseStartAndEndAreLogged()
    {
        var logger = new CaptureLogger();
        using var handler = new SequenceHandler(Reply.Fail(429, TooFast, TimeSpan.FromSeconds(1)), Reply.Ok(Users));
        using var api = CreateApi(handler, logger: logger);

        await api.Search.UsersAsync("nick");
        logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning && entry.Text.Contains("paused for 1 s"));

        var deadline = Stopwatch.StartNew();
        while (!logger.Entries.Any(entry => entry.Text.Contains("pause ended")) && deadline.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(50);
        logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Information && entry.Text.Contains("pause ended after"));
    }

    [TestMethod]
    public async Task ThrowingLoggerAtThePauseEndDoesNotCrashTheProcess()
    {
        // An exception thrown from a timer callback is unhandled and would terminate the test host.
        var logger = new ThrowingLogger();
        using var handler = new SequenceHandler(Reply.Fail(429, TooFast, TimeSpan.FromSeconds(1)), Reply.Ok(Users));
        using var api = CreateApi(handler, logger: logger);

        await api.Search.UsersAsync("nick");
        await Task.Delay(TimeSpan.FromSeconds(2));
        logger.Calls.Should().BeGreaterThan(0);
        // The pause has ended although its log failed, so the next request is sent.
        await api.Search.UsersAsync("nick");
        handler.CallCount.Should().Be(2);
    }

    [TestMethod]
    public async Task ClientSideLimitRefusesRequestsOverTheLimit()
    {
        using var handler = new SequenceHandler(Reply.Ok(Users));
        using var api = CreateApi(handler, builder => builder.WithRateLimit(2, TimeSpan.FromMinutes(1)));

        (await api.Search.UsersAsync("nick")).IsOk().Should().BeTrue();
        (await api.Search.UsersAsync("nick")).IsOk().Should().BeTrue();
        var third = await api.Search.UsersAsync("nick");
        third.Error.Code.Should().Be(429);
        third.Error.Type.Should().Be(ErrorType.RateLimit);
        third.Error.RetryAfter.Should().BeGreaterThan(TimeSpan.FromSeconds(55));
        handler.CallCount.Should().Be(2);
    }

    [TestMethod]
    public async Task ClientSideLimitWaitsForAFreeSlotInWaitMode()
    {
        using var handler = new SequenceHandler(Reply.Ok(Users));
        using var api = CreateApi(handler, builder => builder.WithRateLimit(2, TimeSpan.FromMilliseconds(600))
            .WithRateLimitMaxWait(TimeSpan.FromSeconds(5)));

        var watch = Stopwatch.StartNew();
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => api.Search.UsersAsync("nick")));
        results.Should().OnlyContain(result => result.IsOk());
        // Five requests at two per 600 ms: the last pair starts after two full windows.
        watch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(1100));
        handler.CallCount.Should().Be(5);
    }

    [TestMethod]
    public async Task UnboundedMaximumWaitIsAccepted()
    {
        using var handler = new SequenceHandler(Reply.Ok(Users));
        using var api = CreateApi(handler, builder => builder.WithRateLimitMaxWait(TimeSpan.MaxValue)
            .WithRateLimit(5, TimeSpan.FromMinutes(1)));

        // The first call may be the first use of the monotonic clock; overflow needs it to have advanced.
        (await api.Search.UsersAsync("nick")).IsOk().Should().BeTrue();
        await Task.Delay(20);
        (await api.Search.UsersAsync("nick")).IsOk().Should().BeTrue();
        api.Config.RateLimitMaxWait.Should().Be(TimeSpan.MaxValue);
    }

    [TestMethod]
    public void InvalidRateLimitSettingsAreRejected()
    {
        ConfigBuilder Builder() => new ConfigBuilder().WithUri("https://new.shinden.test/")
            .WithAuth(new Auth { UserAgent = "agent" });

        Builder().WithRateLimitMaxWait(TimeSpan.FromSeconds(-1)).Invoking(builder => builder.Build())
            .Should().Throw<ArgumentOutOfRangeException>();
        Builder().WithRateLimitRetries(-1).Invoking(builder => builder.Build())
            .Should().Throw<ArgumentOutOfRangeException>();
        Builder().WithRateLimit(-1, TimeSpan.FromMinutes(1)).Invoking(builder => builder.Build())
            .Should().Throw<ArgumentOutOfRangeException>();
        Builder().WithRateLimit(10, TimeSpan.Zero).Invoking(builder => builder.Build())
            .Should().Throw<ArgumentOutOfRangeException>();
        Builder().WithRateLimit(10, TimeSpan.MaxValue).Invoking(builder => builder.Build())
            .Should().Throw<ArgumentOutOfRangeException>();
        Builder().WithRateLimit(0, TimeSpan.Zero).Build().RateLimitRequests.Should().Be(0);

        // A configuration created directly is validated by the client too.
        FluentActions.Invoking(() => new Shinden(new Config
        {
            Uri = new Uri("https://new.shinden.test/"),
            Auth = new Auth { UserAgent = "agent" },
            RateLimitRetries = -1
        })).Should().Throw<ArgumentOutOfRangeException>();
    }

    [DataTestMethod]
    [DataRow("1.5", 1500, 2)]
    [DataRow("\"2.5\"", 2500, 3)]
    [DataRow("-1", null, null)]
    public async Task RetryAfterFromTheBodyIsReadWithoutCultureDependence(string value, int? milliseconds, int? seconds)
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
            using var handler = new SequenceHandler(Reply.Fail(429, $$"""{"retry_after":{{value}}}"""));
            using var api = CreateApi(handler);
            var error = (await api.Search.UsersAsync("nick")).Error;
            error.RetryAfter.Should().Be(milliseconds is { } ms ? TimeSpan.FromMilliseconds(ms) : null);
            error.RetryAfterSeconds.Should().Be(seconds);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    private sealed record Reply(int Status, string Body, TimeSpan? Delta, DateTimeOffset? Date)
    {
        internal static Reply Ok(string body) => new(200, body, null, null);
        internal static Reply Fail(int status, string body, TimeSpan? delta = null, DateTimeOffset? date = null)
            => new(status, body, delta, date);
    }

    /// <summary>Returns the replies in order and then keeps repeating the last one.</summary>
    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Reply[] _replies;
        private int _calls;

        internal SequenceHandler(params Reply[] replies) => _replies = replies;

        internal int CallCount => Volatile.Read(ref _calls);
        internal ConcurrentQueue<HttpMethod> MethodQueue { get; } = new();
        internal ConcurrentQueue<string> HostQueue { get; } = new();
        internal IEnumerable<HttpMethod> Methods => MethodQueue;
        internal IEnumerable<string> Hosts => HostQueue;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref _calls) - 1;
            MethodQueue.Enqueue(request.Method);
            HostQueue.Enqueue(request.RequestUri!.Host);
            var reply = _replies[Math.Min(index, _replies.Length - 1)];
            var response = new HttpResponseMessage((HttpStatusCode)reply.Status) { Content = new StringContent(reply.Body) };
            if (reply.Delta is { } delta)
                response.Headers.RetryAfter = new RetryConditionHeaderValue(delta);
            else if (reply.Date is { } date)
                response.Headers.RetryAfter = new RetryConditionHeaderValue(date);
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingLogger : ILogger<Shinden>
    {
        private int _calls;
        internal int Calls => Volatile.Read(ref _calls);
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
        public bool IsEnabled(LogLevel level) => level == LogLevel.Information;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Interlocked.Increment(ref _calls);
            throw new InvalidOperationException("The log sink is unavailable.");
        }
    }

    private sealed record Entry(LogLevel Level, string Text);

    private sealed class CaptureLogger : ILogger<Shinden>
    {
        private readonly ConcurrentQueue<Entry> _entries = new();
        internal IReadOnlyList<Entry> Entries => _entries.ToArray();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _entries.Enqueue(new Entry(level, formatter(state, exception)));
    }
}
