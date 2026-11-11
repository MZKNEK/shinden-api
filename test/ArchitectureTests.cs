using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging;
using ShindenAPI.Credentials;

namespace ShindenAPI.Test;

[TestClass]
public class ArchitectureTests
{
    [TestMethod]
    public async Task ExternalHttpClientDefaultsRemainUnchanged()
    {
        using var handler = new HeaderHandler();
        using var client = new HttpClient(handler);
        using var api = new Shinden(
            new Uri("https://fake.shinden.test/"),
            new Auth
            {
                Token = "token",
                UserAgent = "test-agent"
            },
            client: client);

        await api.Titles.InfoAsync(1);

        client.DefaultRequestHeaders.Should().NotContain(x =>
            x.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase));
        client.DefaultRequestHeaders.Should().NotContain(x =>
            x.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase));
        handler.RequestHadAcceptLanguage.Should().BeTrue();
        handler.RequestHadAccept.Should().BeTrue();
        handler.RequestHadUserAgent.Should().BeTrue();
        handler.RequestHadAuthorization.Should().BeTrue();
    }

    [DataTestMethod]
    [DataRow(503, 0)]
    [DataRow(429, 1)]
    public async Task RetryPolicyRetriesTransientGetFailures(int status, int rateLimitRetries)
    {
        using var handler = new RetryHandler(
            new HttpStatusCode[] { (HttpStatusCode)status, HttpStatusCode.OK },
            "[{\"user_id\":85,\"name\":\"retry\",\"rank\":\"Tester\",\"avatar\":true}]");
        using var api = new Shinden(config => config
            .WithUri("https://fake.shinden.test/")
            .WithMaxRetries(1)
            .WithRateLimitRetries(rateLimitRetries)
            .WithRetryDelay(TimeSpan.Zero)
            .WithAuth(new Auth { Token = "token", UserAgent = "test-agent" })
            .Build(),
            handler: handler);

        var result = await api.AskAsync(new Queries.Search.User("retry"));

        result.IsOk().Should().BeTrue();
        handler.CallCount.Should().Be(2);
        handler.AuthorizationValues.Should().Equal("Bearer token", "Bearer token");
    }

    [TestMethod]
    public async Task LongRetryAfterReturnsRateLimitInsteadOfTimeout()
    {
        using var handler = new RecordingHandler
        {
            StatusCode = (HttpStatusCode)429, RetryAfter = 60,
            Response = """{"code":"too_fast","message":"Slow down"}"""
        };
        using var api = new Shinden(config => config.WithUri("https://fake.shinden.test/")
            .WithRequestTimeout(TimeSpan.FromSeconds(1)).WithMaxRetries(2)
            .WithAuth(new Auth { UserAgent = "test" }).Build(), handler: handler);
        var result = await api.Users.StatsAsync(42);
        result.Error.Code.Should().Be(429);
        result.Error.ApiCode.Should().Be("too_fast");
        result.Error.RetryAfterSeconds.Should().Be(60);
        handler.CallCount.Should().Be(1);
    }

    [TestMethod]
    public async Task ExternalTransportsRemainOwnedByCaller()
    {
        using var handler = new RecordingHandler { Response = """{"title":{"title_id":42}}""" };
        using (var api = new Shinden(new Uri("https://fake.shinden.test/"),
            new Auth { UserAgent = "test" }, handler: handler))
            (await api.Titles.InfoAsync(42)).IsOk().Should().BeTrue();
        handler.WasDisposed.Should().BeFalse();

        using var client = new HttpClient(handler, disposeHandler: false);
        using (var api = new Shinden(new Uri("https://fake.shinden.test/"),
            new Auth { UserAgent = "test" }, client: client))
            (await api.Titles.InfoAsync(42)).IsOk().Should().BeTrue();
        using var response = await client.GetAsync("https://fake.shinden.test/health");
        response.IsSuccessStatusCode.Should().BeTrue();
        handler.WasDisposed.Should().BeFalse();
    }

    [TestMethod]
    public async Task ConfiguredRequestTimeoutReturnsTypedTimeoutError()
    {
        using var handler = new BlockingHandler();
        using var api = new Shinden(config => config
            .WithUri("https://fake.shinden.test/")
            .WithRequestTimeout(TimeSpan.FromMilliseconds(10))
            .WithAuth(new Auth { Token = "token", UserAgent = "test-agent" })
            .Build(),
            handler: handler);

        var result = await api.AskAsync(new Queries.Search.User("timeout"));

        result.IsOk().Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Timeout);
    }

    [TestMethod]
    public async Task ConfiguredRequestTimeoutCoversResponseBodyRead()
    {
        using var handler = new SlowResponseBodyHandler();
        using var api = new Shinden(config => config
            .WithUri("https://fake.shinden.test/")
            .WithRequestTimeout(TimeSpan.FromMilliseconds(10))
            .WithAuth(new Auth { Token = "token", UserAgent = "test-agent" })
            .Build(),
            handler: handler);

        var result = await api.AskAsync(new Queries.Search.User("timeout"));

        result.IsOk().Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Timeout);
    }

    [TestMethod]
    public async Task ApiTokenIsNeverWrittenToLogs()
    {
        const string secretToken = "super-secret-token-value";
        var logger = new CapturingLogger();
        using var handler = new EchoingErrorHandler();
        using var api = new Shinden(
            new Uri("https://fake.shinden.test/"),
            new Auth { Token = secretToken, UserAgent = "test-agent" },
            logger,
            handler: handler);

        // Servers may reflect authentication headers in error bodies.
        var result = await api.AskAsync(new Queries.Search.User("probe"));

        result.IsOk().Should().BeFalse();
        logger.Messages.Should().NotBeEmpty();
        logger.Messages.Should().NotContain(message =>
            message.Contains(secretToken) || message.Contains(Uri.EscapeDataString(secretToken)));
    }

    private sealed class HeaderHandler : HttpMessageHandler
    {
        internal bool RequestHadAcceptLanguage { get; private set; }
        internal bool RequestHadAccept { get; private set; }
        internal bool RequestHadUserAgent { get; private set; }
        internal bool RequestHadAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestHadAcceptLanguage = request.Headers.Contains("Accept-Language");
            RequestHadAccept = request.Headers.Contains("Accept");
            RequestHadUserAgent = request.Headers.Contains("User-Agent");
            RequestHadAuthorization = request.Headers.Contains("Authorization");
            return Task.FromResult(Json("{}"));
        }
    }

    private sealed class RetryHandler : HttpMessageHandler
    {
        private readonly IReadOnlyList<HttpStatusCode> _statuses;
        private readonly string _content;

        internal RetryHandler(IReadOnlyList<HttpStatusCode> statuses, string content)
        {
            _statuses = statuses;
            _content = content;
        }

        internal int CallCount { get; private set; }
        internal List<string?> AuthorizationValues { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Math.Min(CallCount, _statuses.Count - 1);
            CallCount++;
            AuthorizationValues.Add(request.Headers.Authorization?.ToString());
            var response = new HttpResponseMessage(_statuses[index])
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            };
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
            return Task.FromResult(response);
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Json("{}");
        }
    }

    private sealed class SlowResponseBodyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new SlowResponseContent()
            });
    }

    private sealed class SlowResponseContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => Task.Delay(Timeout.InfiniteTimeSpan);

        protected override Task SerializeToStreamAsync(
            Stream stream, TransportContext? context, CancellationToken cancellationToken)
            => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class EchoingErrorHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(request.Headers.Authorization!.ToString(), Encoding.UTF8, "text/plain")
            });
    }

    private sealed class CapturingLogger : ILogger<Shinden>
    {
        internal List<string> Messages { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            internal static readonly NullScope Instance = new();
            private NullScope() { }
            public void Dispose() { }
        }
    }

    private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };
}
