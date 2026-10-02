using System.Net;
using System.Net.Http;
using ShindenAPI.Credentials;
using ShindenAPI.Models;

namespace ShindenAPI.Test;

[TestClass]
public class RequestContractTests
{
    [TestMethod]
    public async Task LoginFailureIsReturnedAsTypedError()
    {
        using var handler = new CapturingHandler(HttpStatusCode.Unauthorized, "invalid credentials");
        using var api = new Shinden(
            new Uri("https://fake.shinden.test/api"),
            new UserAuth
            {
                Token = "token",
                UserAgent = "test-agent",
                Nickname = "user",
                Password = "password"
            },
            handler: handler);

        var result = await api.Users.LoginResultAsync();

        result.IsOk().Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Http);
        result.GetCode().Should().Be((int)HttpStatusCode.Unauthorized);
        result.TryGet(out _).Should().BeFalse();
    }

    [TestMethod]
    public void FailedResultDoesNotLookLikeSuccess()
    {
        var result = ErrorOr<string>.Failure(
            new Error { Type = ErrorType.Exception, Code = 500, Message = "failed" });

        result.IsOk().Should().BeFalse();
        result.IsError().Should().BeTrue();
        result.Invoking(value => value.Get())
            .Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public async Task ApiKeyIsEscapedAndTrailingBaseSlashIsNormalized()
    {
        using var handler = new CapturingHandler();
        using var api = new Shinden(
            new Uri("https://fake.shinden.test/api/"),
            new Auth { Token = "token&next=value", UserAgent = "test-agent" },
            handler: handler);

        await api.AskAsync(new Queries.User.FavouriteAdd(x => x
            .WithUserId(1)
            .WithTitleId(2)
            .Build()));

        handler.LastRequestUri!.AbsolutePath.Should().Be("/api/userlist/1/fav");
        handler.LastRequestUri.Query.Should().Contain("api_key=token%26next%3Dvalue");
    }

    [TestMethod]
    public async Task TimeoutIsReturnedAsTypedError()
    {
        using var handler = new BlockingHandler();
        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMilliseconds(10)
        };
        using var api = new Shinden(
            new Uri("https://fake.shinden.test/api"),
            new Auth { Token = "token", UserAgent = "test-agent" },
            client: client);

        var result = await api.AskAsync(new Queries.Search.User("test"));

        result.IsOk().Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Timeout);
    }

    [TestMethod]
    public void ExternalHandlerRemainsOwnedByCaller()
    {
        var handler = new TrackingHandler();
        using (var api = new Shinden(
            new Uri("https://fake.shinden.test/api"),
            new Auth { Token = "token", UserAgent = "test-agent" },
            handler: handler))
        {
        }

        handler.WasDisposed.Should().BeFalse();
        handler.Dispose();
    }

    [TestMethod]
    public async Task AnimeSearchAddsOneExplicitOffset()
    {
        using var handler = new CapturingHandler();
        using var api = CreateApi(handler);

        var result = await api.Search.AnimeAsync(new Queries.Search.AnimeSearchOptionsBuilder()
            .WithQuery("test")
            .WithOffset(20)
            .Build());

        result.IsOk().Should().BeTrue();
        handler.LastForm!.Split('&').Count(value => value.StartsWith("offset=")).Should().Be(1);
        handler.LastForm.Should().Contain("offset=20");
    }

    [TestMethod]
    public async Task EnumActionsUseApiValues()
    {
        using var handler = new CapturingHandler();
        using var api = CreateApi(handler);

        var statusResult = await api.Users.ChangeTitleStatusAsync(1, 2, TitleListStatus.OnHold);
        statusResult.IsOk().Should().BeTrue();
        handler.LastForm.Should().Contain("status=hold");

        var ratingResult = await api.Users.RateAnimeAsync(2, AnimeRatingCategory.Characters, 8);
        ratingResult.IsOk().Should().BeTrue();
        handler.LastForm.Should().Contain("type=titlecahracters");
        handler.LastForm.Should().Contain("value=8");
    }

    private static Shinden CreateApi(CapturingHandler handler) => new(
        new Uri("https://fake.shinden.test/api"),
        new Auth { Token = "token", UserAgent = "test-agent" },
        handler: handler);

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _response;

        internal CapturingHandler(
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string response = "{}")
        {
            _statusCode = statusCode;
            _response = response;
        }

        internal string? LastForm { get; private set; }
        internal Uri? LastRequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastForm = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_response)
            };
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class TrackingHandler : HttpMessageHandler
    {
        internal bool WasDisposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
