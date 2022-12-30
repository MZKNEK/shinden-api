global using FluentAssertions;
using System.Runtime.CompilerServices;
using System.Text;

namespace ShindenAPI.Test;

public class TestBase : VerifyBase
{
    protected static string _uri = default!;
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
    }
}
