global using FluentAssertions;
global using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShindenAPI.Test;

[TestClass]
public class TestBase
{
    protected static Shinden _api = default!;

    [AssemblyInitialize]
    public static void Setup(TestContext testContext)
    {
        _api = new Shinden(config => config.WithUri("https://fake.shinden.test/")
            .WithAuth(auth => auth.WithToken("shk_test").WithUserAgent("PTestShinden").Build()).Build(),
            handler: new FakeApiHandler());
    }

    [AssemblyCleanup]
    public static void Cleanup() => _api.Dispose();
}
