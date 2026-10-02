using Microsoft.Extensions.DependencyInjection;
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
}
