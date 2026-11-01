using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using System.Net.Http;
using ShindenAPI.Credentials;

namespace ShindenAPI.Test;

[TestClass]
public class DependencyInjectionTests
{
    [TestMethod]
    public void AddShindenRegistersFactoryBackedClient()
    {
        using var provider = new ServiceCollection()
            .AddShinden(config => config
                .WithUri("https://api.example.test/")
                .WithAuth(new Auth { Token = "token", UserAgent = "test-agent" })
                .Build())
            .BuildServiceProvider();

        var client = provider.GetRequiredService<Shinden>();

        client.Should().NotBeNull();
    }

    [TestMethod]
    public void FactoryTransportDoesNotShareAutomaticCookiesAcrossApis()
    {
        var inspection = new InspectHandler();
        using var provider = new ServiceCollection()
            .AddSingleton<IHttpMessageHandlerBuilderFilter>(inspection)
            .AddShinden(config => config.WithUri("https://shinden.pl/")
                .WithAuth(new Auth { UserAgent = "test" })
                .WithLegacyAuth(new Auth { Token = "old-key", UserAgent = "test" }).Build())
            .BuildServiceProvider();
        provider.GetRequiredService<Shinden>();
        inspection.Handler.Should().NotBeNull();
        inspection.Handler!.UseCookies.Should().BeFalse();
        inspection.Handler.AllowAutoRedirect.Should().BeFalse();
    }

    private sealed class InspectHandler : IHttpMessageHandlerBuilderFilter
    {
        internal HttpClientHandler? Handler { get; private set; }
        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
            => builder => { next(builder); Handler = builder.PrimaryHandler as HttpClientHandler; };
    }
}
