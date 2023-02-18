using Microsoft.Extensions.Logging;

namespace ShindenAPI.Test;

public class TestBaseUserCred : TestBase
{
    [TestInitialize]
    public void ReCreateApiManager()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("Microsoft", LogLevel.Critical)
                .AddFilter("System", LogLevel.Critical)
                .AddFilter("ShindenAPI.Shinden", LogLevel.Trace)
                .AddSimpleConsole();
        });

        _api = new Shinden(x => x.WithUri(new Uri(_uri))
            .WithAuth(x => x.WithToken(_token)
                .WithPassword(_token)
                .WithUsername("tsanapi")
                .WithUserAgent("PTestShinden")
                .WithMarmolade(_secret).Build()).Build(),
            loggerFactory.CreateLogger<Shinden>());
    }
}
