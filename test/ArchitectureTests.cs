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
            new Uri("https://fake.shinden.test/api"),
            new Auth
            {
                Token = "token",
                UserAgent = "test-agent",
                Marmolade = "x-marmolade"
            },
            client: client);

        await api.AskAsync(new Queries.User.FavouriteAdd(x => x
            .WithUserId(1)
            .WithTitleId(2)
            .Build()));

        client.DefaultRequestHeaders.Should().NotContain(x =>
            x.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase));
        client.DefaultRequestHeaders.Should().NotContain(x =>
            x.Key.Equals("x-marmolade", StringComparison.OrdinalIgnoreCase));
        handler.RequestHadAcceptLanguage.Should().BeTrue();
        handler.RequestHadAccept.Should().BeTrue();
        handler.RequestHadUserAgent.Should().BeTrue();
        handler.RequestHadMarmolade.Should().BeTrue();
    }

    [TestMethod]
    public async Task SessionExpirationUsesConfiguredClock()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var api = new Shinden(config => config
            .WithUri("https://fake.shinden.test/api")
            .WithSessionLifetime(TimeSpan.FromMinutes(1))
            .WithClock(() => now)
            .WithAuth(new UserAuth
            {
                Token = "token",
                UserAgent = "test-agent",
                Nickname = "user",
                Password = "password"
            })
            .Build(),
            handler: new FakeApiHandler());

        (await api.Users.LoginResultAsync()).IsOk().Should().BeTrue();
        api.Users.IsValidSession().Should().BeTrue();

        now = now.AddMinutes(2);

        api.Users.IsValidSession().Should().BeFalse();
    }

    [TestMethod]
    public async Task RetryPolicyRetriesTransientGetFailures()
    {
        using var handler = new RetryHandler(
            new HttpStatusCode[] { HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK },
            "[{\"user_id\":85,\"name\":\"retry\",\"rank\":\"Tester\",\"avatar\":\"18\"}]");
        using var api = new Shinden(config => config
            .WithUri("https://fake.shinden.test/api")
            .WithMaxRetries(1)
            .WithRetryDelay(TimeSpan.Zero)
            .WithAuth(new Auth { Token = "token", UserAgent = "test-agent" })
            .Build(),
            handler: handler);

        var result = await api.AskAsync(new Queries.Search.User("retry"));

        result.IsOk().Should().BeTrue();
        handler.CallCount.Should().Be(2);
    }

    [TestMethod]
    public async Task RetryPolicyDoesNotRetryMutatingRequests()
    {
        using var handler = new RetryHandler(
            new HttpStatusCode[] { HttpStatusCode.ServiceUnavailable }, "{}");
        using var api = new Shinden(config => config
            .WithUri("https://fake.shinden.test/api")
            .WithMaxRetries(3)
            .WithRetryDelay(TimeSpan.Zero)
            .WithAuth(new Auth { Token = "token", UserAgent = "test-agent" })
            .Build(),
            handler: handler);

        var result = await api.AskAsync(new Queries.User.FavouriteAdd(x => x
            .WithUserId(1)
            .WithTitleId(2)
            .Build()));

        result.IsOk().Should().BeFalse();
        handler.CallCount.Should().Be(1);
    }

    [TestMethod]
    public async Task ConfiguredRequestTimeoutReturnsTypedTimeoutError()
    {
        using var handler = new BlockingHandler();
        using var api = new Shinden(config => config
            .WithUri("https://fake.shinden.test/api")
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
            .WithUri("https://fake.shinden.test/api")
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
            new Uri("https://fake.shinden.test/api"),
            new Auth { Token = secretToken, UserAgent = "test-agent" },
            logger,
            handler: handler);

        // The handler below echoes the full request URL - including the api_key query
        // parameter - back as the (failed) response body, to simulate a worst-case server
        // that reflects request details in its error output.
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
        internal bool RequestHadMarmolade { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestHadAcceptLanguage = request.Headers.Contains("Accept-Language");
            RequestHadAccept = request.Headers.Contains("Accept");
            RequestHadUserAgent = request.Headers.Contains("User-Agent");
            RequestHadMarmolade = request.Headers.Contains("x-marmolade");
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

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Math.Min(CallCount, _statuses.Count - 1);
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(_statuses[index])
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            });
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
                Content = new StringContent(request.RequestUri!.ToString(), Encoding.UTF8, "text/plain")
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
