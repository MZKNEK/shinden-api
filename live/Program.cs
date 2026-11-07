using Newtonsoft.Json;

namespace ShindenAPI.LiveTests;

internal static class Program
{
    private const string Help = """
    Shinden.NET live tests — manual execution only.

    Validate configuration (no network requests):
      dotnet run --project live/Shinden.NET.LiveTests.csproj -- --validate --config live/live-settings.local.json

    Read checks and optional PHP login:
      dotnet run --project live/Shinden.NET.LiveTests.csproj -- --run-live --config live/live-settings.local.json

    Only checks whose name starts with a prefix (others are reported as SKIP and send no requests):
      dotnet run --project live/Shinden.NET.LiveTests.csproj -- --run-live --only compat.current-season --config live/live-settings.local.json

    Write scenarios enabled in the JSON configuration:
      dotnet run --project live/Shinden.NET.LiveTests.csproj -- --run-live --allow-writes --config live/live-settings.local.json

    The JSON report is written next to the configuration, in the reports directory.
    It excludes keys, passwords, cookies and raw responses. SKIP means a check was skipped.
    Exit codes: 0 no failed checks, 1 check failed, 2 configuration/CI error, 130 cancelled.
    """;

    internal static bool IsCi(Func<string, string?> environment)
    {
        var markers = new[] { "CI", "GITHUB_ACTIONS", "GITLAB_CI", "TF_BUILD", "APPVEYOR", "TEAMCITY_VERSION", "JENKINS_URL", "BUILD_BUILDID" };
        return markers.Any(name => environment(name) is { Length: > 0 } value
            && !value.Equals("false", StringComparison.OrdinalIgnoreCase) && value != "0");
    }

#if LIVE_RUNNER
    private static Task<int> Main(string[] args) => RunAsync(args);
#endif

    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help")) { Console.WriteLine(Help); return 0; }
        try
        {
            var (run, allowWrites, configPath, only) = ParseArguments(args);
            if (run && IsCi(Environment.GetEnvironmentVariable))
                throw new SettingsException("Live tests are blocked in CI environments.");
            var settings = LiveSettings.Load(configPath);
            settings.Validate(allowWrites);
            if (!run)
            {
                Console.WriteLine("Configuration is valid. No requests were sent.");
                return 0;
            }
            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
            Console.CancelKeyPress += cancel;
            var runner = new LiveRunner(settings, Console.Out, only: only);
            var exitCode = 0;
            try { await runner.RunAsync(cancellation.Token); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { exitCode = 130; }
            finally
            {
                Console.CancelKeyPress -= cancel;
                var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(configPath))!, "reports");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, $"live-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
                await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(runner.Report, LiveSettings.JsonSettings));
                Console.WriteLine($"PASS: {runner.Report.Passed}, FAIL: {runner.Report.Failed}, SKIP: {runner.Report.Skipped}");
                Console.WriteLine($"Report: {path}");
            }
            return exitCode != 0 ? exitCode : runner.Report.Failed > 0 ? 1 : 0;
        }
        catch (SettingsException error) { Console.Error.WriteLine(error.Message); return 2; }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Could not complete the tests ({error.GetType().Name}). Potentially sensitive details were omitted.");
            return 2;
        }
    }

    internal static (bool Run, bool AllowWrites, string ConfigPath, string? Only) ParseArguments(string[] args)
    {
        var run = false;
        var validate = false;
        var writes = false;
        var path = "live/live-settings.local.json";
        string? only = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--run-live": run = true; break;
                case "--validate": validate = true; break;
                case "--allow-writes": writes = true; break;
                case "--config" when i + 1 < args.Length: path = args[++i]; break;
                case "--only" when i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]): only = args[++i]; break;
                default: throw new SettingsException("Unknown argument or missing value after --config/--only. Use --help.");
            }
        }
        if (run == validate) throw new SettingsException("Select exactly one mode: --run-live or --validate.");
        return (run, writes, path, only);
    }
}
