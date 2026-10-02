using ShindenAPI.Credentials;

namespace ShindenAPI.Test;

[TestClass]
public class ConfigurationTests
{
    [TestMethod]
    public void AuthBuilderRejectsPartialUserCredentials()
    {
        var builder = new AuthBuilder()
            .WithToken("token")
            .WithUserAgent("test-agent")
            .WithUsername("user");

        builder.Invoking(value => value.Build())
            .Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void ConfigBuilderRequiresUriAndAuth()
    {
        var builder = new ConfigBuilder();

        builder.Invoking(value => value.Build())
            .Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void ConfigBuilderRejectsRelativeUri()
    {
        var builder = new ConfigBuilder();

        builder.Invoking(value => value.WithUri("api"))
            .Should().Throw<UriFormatException>();
    }

    [TestMethod]
    public async Task ConfigBuilderAppliesCustomSiteAndImageHosts()
    {
        using var api = new Shinden(config => config
            .WithUri("https://api.example.test/api")
            .WithSiteUri("https://site.example.test/base/")
            .WithImageServerUri("https://images.example.test/cdn/")
            .WithAuth(auth => auth
                .WithToken("token")
                .WithUserAgent("test-agent")
                .Build())
            .Build(),
            handler: new FakeApiHandler());

        var result = await api.AskAsync(new Queries.Search.User("test"));
        var user = result.Get().Single();

        user.ProfileUrl.Should().Be("https://site.example.test/base/user/85");
        user.AvatarUrl.Should().Be("https://images.example.test/cdn/cdn1/avatars/225x350/85.jpg?v18");
    }
}