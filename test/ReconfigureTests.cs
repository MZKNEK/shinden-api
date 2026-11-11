using System.Net;
using ShindenAPI.Credentials;

namespace ShindenAPI.Test;

[TestClass]
public class ReconfigureTests
{
    private static Shinden CreateApi(RecordingHandler handler) => new(config => config
        .WithUri("https://first.shinden.test/")
        .WithAuth(new Auth { Token = "shk_first", UserAgent = "first-agent" })
        .WithMaxRetries(0).WithRetryDelay(TimeSpan.Zero).Build(), handler: handler);

    [TestMethod]
    public async Task ReconfigureChangesAddressAndTokenOnTheSameClient()
    {
        using var handler = new RecordingHandler { Response = "[]" };
        using var api = CreateApi(handler);
        var search = api.Search;
        (await search.UsersAsync("nick")).IsOk().Should().BeTrue();
        handler.Uri.Host.Should().Be("first.shinden.test");
        handler.Authorization.Should().Be("Bearer shk_first");

        api.Reconfigure(builder => builder
            .WithUri("https://second.shinden.test/")
            .WithAuth(new Auth { Token = "shk_second", UserAgent = "second-agent" })
            .Build());

        (await search.UsersAsync("nick")).IsOk().Should().BeTrue();
        api.Search.Should().BeSameAs(search);
        handler.Uri.Host.Should().Be("second.shinden.test");
        handler.Authorization.Should().Be("Bearer shk_second");
        api.Config.Uri.Should().Be(new Uri("https://second.shinden.test/"));
    }

    [TestMethod]
    public async Task BuilderStartsFromCurrentConfigurationSoOnlyChangesAreNeeded()
    {
        using var handler = new RecordingHandler { StatusCode = HttpStatusCode.ServiceUnavailable };
        using var api = CreateApi(handler);
        await api.Search.UsersAsync("nick");
        handler.CallCount.Should().Be(1);

        api.Reconfigure(builder => builder.WithMaxRetries(2).Build());

        await api.Search.UsersAsync("nick");
        handler.CallCount.Should().Be(1 + 3);
        handler.Authorization.Should().Be("Bearer shk_first");
        api.Config.MaxRetries.Should().Be(2);
        api.Config.Uri.Should().Be(new Uri("https://first.shinden.test/"));
    }

    [TestMethod]
    public void ConfigBuilderCopiesEveryValueFromAnExistingConfiguration()
    {
        Func<DateTimeOffset> clock = () => DateTimeOffset.UnixEpoch;
        var original = new ConfigBuilder()
            .WithUri("https://api.shinden.test/").WithAuth(new Auth { Token = "t", UserAgent = "a" })
            .WithLegacyUri("https://php.shinden.test/").WithLegacyAuth(new Auth { Token = "old", UserAgent = "b" })
            .WithSiteUri("https://site.test/").WithImageServerUri("https://img.test/")
            .WithRequestTimeout(TimeSpan.FromSeconds(7)).WithMaxRetries(4).WithRetryDelay(TimeSpan.FromSeconds(1))
            .WithSessionLifetime(TimeSpan.FromMinutes(5)).WithClock(clock)
            .WithRateLimitMaxWait(TimeSpan.FromSeconds(3)).WithRateLimitRetries(1)
            .WithRateLimit(290, TimeSpan.FromSeconds(61)).Build();

        new ConfigBuilder(original).Build().Should().BeEquivalentTo(original);
    }

    [TestMethod]
    public async Task InvalidConfigurationChangesNothing()
    {
        using var handler = new RecordingHandler { Response = "[]" };
        using var api = CreateApi(handler);
        var before = api.Config;

        api.Invoking(value => value.Reconfigure(builder => builder.WithMaxRetries(-1).Build()))
            .Should().Throw<ArgumentOutOfRangeException>();
        // The new API part is valid, the PHP part is not: neither may be applied.
        api.Invoking(value => value.Reconfigure(new Config
        {
            Uri = new Uri("https://second.shinden.test/"),
            Auth = new Auth { Token = "shk_second", UserAgent = "second-agent" },
            LegacyAuth = new UserAuth { Nickname = "user", Password = "", UserAgent = "php-agent" }
        })).Should().Throw<ArgumentException>();

        api.Config.Should().BeSameAs(before);
        (await api.Search.UsersAsync("nick")).IsOk().Should().BeTrue();
        handler.Uri.Host.Should().Be("first.shinden.test");
        handler.Authorization.Should().Be("Bearer shk_first");
    }

    [TestMethod]
    public void NullConfigurationIsRejected()
    {
        using var api = CreateApi(new RecordingHandler());
        api.Invoking(value => value.Reconfigure((Config)null!)).Should().Throw<ArgumentNullException>();
        api.Invoking(value => value.Reconfigure((Func<ConfigBuilder, Config>)null!)).Should().Throw<ArgumentNullException>();
        api.Invoking(value => value.Reconfigure(_ => null!)).Should().Throw<InvalidOperationException>();
    }
}
