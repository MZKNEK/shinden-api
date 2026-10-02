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
    private RequestManager _manager;

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
    /// <summary>Experimental operations.</summary>
    public ExperimentalClient Experimental { get; }

    public Shinden(Func<ConfigBuilder, Config> config, ILogger<Shinden> logger = default!,
        HttpMessageHandler handler = default!, HttpClient client = default!)
        : this((config ?? throw new ArgumentNullException(nameof(config)))(new ConfigBuilder()),
            logger, handler, client) {}

    public Shinden(Config config, ILogger<Shinden> logger = default!,
        HttpMessageHandler handler = default!, HttpClient client = default!)
        : this((config ?? throw new ArgumentNullException(nameof(config))).Uri,
            config.Auth, logger, handler, client,
            new ShindenUrlResolver(config.Uri, config.SiteUri, config.ImageServerUri),
            config.RequestTimeout, config.SessionLifetime, config.Clock,
            config.MaxRetries, config.RetryDelay) {}

    public Shinden(Uri uri, Auth auth, ILogger<Shinden> logger = default!,
        HttpMessageHandler handler = default!, HttpClient client = default!)
        : this(uri, auth, logger, handler, client, null,
            Config.DefaultRequestTimeout, Config.DefaultSessionLifetime, Config.DefaultClock,
            2, Config.DefaultRetryDelay) {}

    private Shinden(Uri uri, Auth auth, ILogger<Shinden> logger,
        HttpMessageHandler handler, HttpClient client, ShindenUrlResolver urlResolver,
        TimeSpan requestTimeout, TimeSpan sessionLifetime, Func<DateTimeOffset> clock,
        int maxRetries, TimeSpan retryDelay)
    {
        _manager = new(uri, auth, logger, handler, client, urlResolver,
            requestTimeout, sessionLifetime, clock, maxRetries, retryDelay);
        Search = new(_manager);
        Titles = new(_manager);
        Users = new(_manager);
        People = new(_manager);
        Characters = new(_manager);
        Episodes = new(_manager);
        Experimental = new(_manager);
    }

    public Task<ErrorOr<TReturn>> AskAsync<TReturn>(Request<TReturn> query) where TReturn : class
        => AskAsync(query, CancellationToken.None);

    public Task<ErrorOr<TReturn>> AskAsync<TReturn>(Request<TReturn> query,
        CancellationToken cancellationToken) where TReturn : class
        => _manager.MakeQueryAsync(query, cancellationToken: cancellationToken);

    public void Dispose() => _manager.Dispose();

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
