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
        : this((config ?? throw new ArgumentNullException(nameof(config))).Uri,
            config.Auth, logger, handler, client,
            new ShindenUrlResolver(config.Uri, config.SiteUri, config.ImageServerUri),
            config.RequestTimeout,
            config.MaxRetries, config.RetryDelay, config.LegacyUri, config.LegacyAuth,
            config.SessionLifetime, config.Clock) {}

    public Shinden(Uri uri, Auth auth, ILogger<Shinden> logger = default!,
        HttpMessageHandler handler = default!, HttpClient client = default!)
        : this(uri, auth, logger, handler, client, null,
            Config.DefaultRequestTimeout,
            2, Config.DefaultRetryDelay) {}

    private Shinden(Uri uri, Auth auth, ILogger<Shinden> logger,
        HttpMessageHandler handler, HttpClient client, ShindenUrlResolver urlResolver,
        TimeSpan requestTimeout,
        int maxRetries, TimeSpan retryDelay, Uri legacyUri = null, Auth legacyAuth = null,
        TimeSpan sessionLifetime = default, Func<DateTimeOffset> clock = null)
    {
        // UserAuth also preserves the historical PHP-only constructor configuration.
        var phpOnly = legacyAuth is null && auth is UserAuth;
        if (phpOnly) legacyAuth = auth;
        var applicationAuth = phpOnly ? new Auth { UserAgent = auth.UserAgent } : auth;
        _manager = new(phpOnly ? LegacyRequest<object>.NormalizeBaseUri(uri) : uri,
            applicationAuth, logger, handler, client, urlResolver,
            requestTimeout, maxRetries, retryDelay);
        try
        {
            _legacyManager = new(legacyUri ?? uri, legacyAuth ?? new Auth { UserAgent = auth.UserAgent },
                logger, handler, client, urlResolver, requestTimeout, maxRetries, retryDelay,
                legacy: true, sessionLifetime: sessionLifetime, clock: clock);
        }
        catch { _manager.Dispose(); throw; }
        Search = new(_manager);
        Titles = new(_manager, _legacyManager);
        Users = new(_manager, _legacyManager);
        People = new(_legacyManager);
        Characters = new(_manager, _legacyManager);
        Episodes = new(_manager);
        PocketWaifu = new(_manager);
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
