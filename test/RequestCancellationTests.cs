using System.Net.Http;
using ShindenAPI.Credentials;

namespace ShindenAPI.Test;

[TestClass]
public class RequestCancellationTests
{
    [TestMethod]
    public async Task AskAsyncPropagatesCancellation()
    {
        using var handler = new BlockingHandler();
        using var api = new Shinden(
            new Uri("http://fake.shinden.test/api"),
            new Auth { Token = "test-token", UserAgent = "test-agent" },
            handler: handler);
        using var cancellation = new CancellationTokenSource();

        var request = api.AskAsync(new Queries.Search.User("test"), cancellation.Token);
        await handler.Started;
        cancellation.Cancel();

        await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () => await request);
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<bool> _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task Started => _started.Task;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _started.SetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }
    }
}
