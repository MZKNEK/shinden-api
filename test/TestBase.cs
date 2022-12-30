global using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace ShindenAPI.Test;

public class TestBase : VerifyBase
{
    protected static string _uri = default!;
    protected static Shinden _api = default!;
    protected static string _token = default!;
    protected static string _secret = default!;


    [AssemblyInitialize]
    public static void Setup(TestContext testContext)
    {
        DerivePathInfo((sourceFile, projectDirectory, type, method) => new(
                directory: Path.Combine(projectDirectory, "verify"),
                typeName: type.Name,
                methodName: method.Name));

        _uri = System.IO.File.ReadAllText("../../../uri");
        _uri.Should().NotBeNullOrEmpty();

        _token = System.IO.File.ReadAllText("../../../token");
        _token.Should().NotBeNullOrEmpty();

        _secret = System.IO.File.ReadAllText("../../../marmolade");
        _secret.Should().NotBeNullOrEmpty();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("Microsoft", LogLevel.Critical)
                .AddFilter("System", LogLevel.Critical)
                .AddFilter("ShindenAPI.Shinden", LogLevel.Trace)
                .AddSimpleConsole();
        });

        _api = new Shinden(x => x.WithUri(_uri)
            .WithAuth(x => x.WithToken(_token)
                .WithUserAgent("PTestShinden")
                .WithMarmolade(_secret).Build()).Build(),
            loggerFactory.CreateLogger<Shinden>());
    }
}
