using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShindenAPI.Credentials;
using ShindenAPI.Queries;

namespace ShindenAPI;

/// <summary>Root client exposing the domain clients used to call the Shinden API.</summary>
public class Shinden : IDisposable
{
    private readonly RequestManager _manager;
    private readonly RequestManager _legacyManager;
    private readonly object _configLock = new();
    private volatile Config _config;

    /// <summary>The configuration currently in use; changed by <see cref="Reconfigure(Config)"/>.</summary>
    public Config Config => _config;

    /// <summary>Search operations.</summary>
    public SearchClient Search { get; }
    /// <summary>Title operations.</summary>
    public TitlesClient Titles { get; }
    /// <summary>User operations.</summary>
    public UsersClient Users { get; }
    /// <summary>People operations.</summary>
    public PeopleClient People { get; }
    /// <summary>Character operations.</summary>
    public CharactersClient Characters { get; }
    /// <summary>Episode operations.</summary>
    public EpisodesClient Episodes { get; }
    /// <summary>Read-only Pocket Waifu copy operations.</summary>
    public PocketWaifuClient PocketWaifu { get; }

    public Shinden(Func<ConfigBuilder, Config> config, ILogger<Shinden> logger = default!,
        HttpMessageHandler handler = default!, HttpClient client = default!)
        : this((config ?? throw new ArgumentNullException(nameof(config)))(new ConfigBuilder()),
            logger, handler, client) {}

    public Shinden(Config config, ILogger<Shinden> logger = default!,
        HttpMessageHandler handler = default!, HttpClient client = default!)
    {
        var target = Resolve(config ?? throw new ArgumentNullException(nameof(config)));
        _manager = new(target.Uri, target.Auth, logger, handler, client, target.UrlResolver,
            config.RequestTimeout, config.MaxRetries, config.RetryDelay, rateLimit: RateLimitOptions.From(config));
        try
        {
            _legacyManager = new(target.LegacyUri, target.LegacyAuth, logger, handler, client, target.UrlResolver,
                config.RequestTimeout, config.MaxRetries, config.RetryDelay,
                legacy: true, sessionLifetime: config.SessionLifetime, clock: config.Clock,
                rateLimit: RateLimitOptions.From(config));
        }
        catch { _manager.Dispose(); throw; }
        _config = config;
        Search = new(_manager);
        Titles = new(_manager, _legacyManager);
        Users = new(_manager, _legacyManager);
        People = new(_legacyManager);
        Characters = new(_manager, _legacyManager);
        Episodes = new(_manager);
        PocketWaifu = new(_manager);
    }

    public Shinden(Uri uri, Auth auth, ILogger<Shinden> logger = default!,
        HttpMessageHandler handler = default!, HttpClient client = default!)
        : this(new Config
        {
            Uri = uri ?? throw new ArgumentNullException(nameof(uri)),
            Auth = auth ?? throw new ArgumentNullException(nameof(auth))
        }, logger, handler, client) {}

    /// <summary>
    /// Changes the configuration of this client in place. The builder starts from the current configuration,
    /// so only the changed values need to be set, e.g. <c>b => b.WithMaxRetries(5).Build()</c>.
    /// </summary>
    public void Reconfigure(Func<ConfigBuilder, Config> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        lock (_configLock)
            Reconfigure(configure(new ConfigBuilder(_config))
                ?? throw new InvalidOperationException("The Shinden configuration cannot be null."));
    }

    /// <summary>
    /// Changes the configuration of this client in place. Requests already in progress finish with the old settings.
    /// The PHP session is kept unless the PHP credentials or address change. The HTTP client, handler and logger stay the same.
    /// </summary>
    public void Reconfigure(Config config)
    {
        if (config is null)
            throw new ArgumentNullException(nameof(config));

        lock (_configLock)
        {
            // Both states are validated before either is applied, so an invalid configuration changes nothing.
            var target = Resolve(config);
            var rateLimit = RateLimitOptions.From(config);
            var main = _manager.Prepare(target.Uri, target.Auth, target.UrlResolver,
                config.RequestTimeout, config.MaxRetries, config.RetryDelay, config.SessionLifetime, config.Clock, rateLimit);
            var legacy = _legacyManager.Prepare(target.LegacyUri, target.LegacyAuth, target.UrlResolver,
                config.RequestTimeout, config.MaxRetries, config.RetryDelay, config.SessionLifetime, config.Clock, rateLimit);
            _manager.Apply(main);
            _legacyManager.Apply(legacy);
            _config = config;
        }
    }

    private sealed record ManagerTargets(Uri Uri, Auth Auth, Uri LegacyUri, Auth LegacyAuth, ShindenUrlResolver UrlResolver);

    private static ManagerTargets Resolve(Config config)
    {
        if (config.Uri is null)
            throw new ArgumentNullException(nameof(config), "An API URI is required.");
        if (config.Auth is null)
            throw new ArgumentNullException(nameof(config), "Authentication settings are required.");

        var auth = config.Auth;
        // UserAuth also preserves the historical PHP-only constructor configuration.
        var phpOnly = config.LegacyAuth is null && auth is UserAuth;
        var legacyAuth = phpOnly ? auth : config.LegacyAuth;
        return new ManagerTargets(
            phpOnly ? LegacyRequest<object>.NormalizeBaseUri(config.Uri) : config.Uri,
            phpOnly ? new Auth { UserAgent = auth.UserAgent } : auth,
            config.LegacyUri ?? config.Uri,
            legacyAuth ?? new Auth { UserAgent = auth.UserAgent },
            new ShindenUrlResolver(config.Uri, config.SiteUri, config.ImageServerUri));
    }

    public Task<ErrorOr<Models.ApiHealth>> HealthAsync(CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Queries.V1.Health(), cancellationToken);

    public Task<ErrorOr<TReturn>> AskAsync<TReturn>(Request<TReturn> query) where TReturn : class
        => AskAsync(query, CancellationToken.None);

    public Task<ErrorOr<TReturn>> AskAsync<TReturn>(Request<TReturn> query,
        CancellationToken cancellationToken) where TReturn : class
        => (query is not null && query.IsLegacy ? _legacyManager : _manager)
            .MakeQueryAsync(query, cancellationToken: cancellationToken);

    public void Dispose()
    {
        _manager.Dispose();
        _legacyManager.Dispose();
    }

    public static string GetUserAvatarUrl(ulong userId) => GetUserAvatarUrl(string.Empty, userId);

    public static string GetUserProfileUrl(ulong userId)
        => ShindenUrlResolver.Default.GetUserProfileUrl(userId);

    public static string GetTitleUrl(ulong titleId)
        => ShindenUrlResolver.Default.GetTitleUrl(titleId);

    public static string GetStaffUrl(ulong id)
        => ShindenUrlResolver.Default.GetStaffUrl(id);

    public static string GetCharacterUrl(ulong id)
        => ShindenUrlResolver.Default.GetCharacterUrl(id);

    internal static string GetUserAvatarUrl(string imageId, ulong userId)
        => ShindenUrlResolver.Default.GetUserAvatarUrl(imageId, userId);

    public static string GetPictureUrl(ulong imageId, bool big = true)
        => ShindenUrlResolver.Default.GetPictureUrl(imageId, big);

    internal static string GetPictureUrl(string imageId, bool big = true)
        => ShindenUrlResolver.Default.GetPictureUrl(imageId, big);
}
