using System;
using ShindenAPI.Credentials;

namespace ShindenAPI;

/// <summary>Fluent builder for validated <see cref="Config"/> instances.</summary>
public class ConfigBuilder
{
    private Auth _auth;
    private Auth _legacyAuth;
    private Uri _legacyUri;
    private TimeSpan _sessionLifetime = Config.DefaultSessionLifetime;
    private Func<DateTimeOffset> _clock = Config.DefaultClock;
    private Uri _uri;
    private Uri _siteUri;
    private Uri _imageServerUri;
    private TimeSpan _requestTimeout = Config.DefaultRequestTimeout;
    private int _maxRetries = 2;
    private TimeSpan _retryDelay = Config.DefaultRetryDelay;

    /// <summary>Creates an empty builder.</summary>
    public ConfigBuilder() { }

    /// <summary>Creates a builder pre-filled with an existing configuration, so only the changed values need to be set.</summary>
    public ConfigBuilder(Config config)
    {
        if (config is null)
            throw new ArgumentNullException(nameof(config));

        _uri = config.Uri;
        _auth = config.Auth;
        _legacyAuth = config.LegacyAuth;
        _legacyUri = config.LegacyUri;
        _sessionLifetime = config.SessionLifetime;
        _clock = config.Clock;
        _siteUri = config.SiteUri;
        _imageServerUri = config.ImageServerUri;
        _requestTimeout = config.RequestTimeout;
        _maxRetries = config.MaxRetries;
        _retryDelay = config.RetryDelay;
    }

    /// <summary>Sets the API base URI.</summary>
    public ConfigBuilder WithUri(string uri)
    {
        if (uri is null)
            throw new ArgumentNullException(nameof(uri));

        _uri = new Uri(uri);
        return this;
    }

    /// <summary>Sets the API base URI.</summary>
    public ConfigBuilder WithUri(Uri uri)
    {
        _uri = uri ?? throw new ArgumentNullException(nameof(uri));
        return this;
    }

    /// <summary>Sets the site URI used for generated links.</summary>
    public ConfigBuilder WithSiteUri(string uri)
    {
        if (uri is null)
            throw new ArgumentNullException(nameof(uri));

        return WithSiteUri(new Uri(uri));
    }

    /// <summary>Sets the site URI used for generated links.</summary>
    public ConfigBuilder WithSiteUri(Uri uri)
    {
        _siteUri = uri ?? throw new ArgumentNullException(nameof(uri));
        return this;
    }

    /// <summary>Sets the image server URI used for generated image links.</summary>
    public ConfigBuilder WithImageServerUri(string uri)
    {
        if (uri is null)
            throw new ArgumentNullException(nameof(uri));

        return WithImageServerUri(new Uri(uri));
    }

    /// <summary>Sets the image server URI used for generated image links.</summary>
    public ConfigBuilder WithImageServerUri(Uri uri)
    {
        _imageServerUri = uri ?? throw new ArgumentNullException(nameof(uri));
        return this;
    }

    /// <summary>Sets the maximum duration of an individual request.</summary>
    public ConfigBuilder WithRequestTimeout(TimeSpan timeout)
    {
        _requestTimeout = timeout;
        return this;
    }


    /// <summary>Sets the number of retries for transient idempotent requests.</summary>
    public ConfigBuilder WithMaxRetries(int maxRetries)
    {
        _maxRetries = maxRetries;
        return this;
    }

    /// <summary>Sets the base delay between transient retries.</summary>
    public ConfigBuilder WithRetryDelay(TimeSpan delay)
    {
        _retryDelay = delay;
        return this;
    }


    /// <summary>Configures authentication settings.</summary>
    public ConfigBuilder WithAuth(Func<AuthBuilder, Auth> auth)
    {
        if (auth is null)
            throw new ArgumentNullException(nameof(auth));

        return WithAuth(auth(new AuthBuilder()));
    }

    /// <summary>Sets authentication settings.</summary>
    public ConfigBuilder WithAuth(Auth auth)
    {
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        return this;
    }

    /// <summary>Sets the PHP API origin, or its historical /api base address.</summary>
    public ConfigBuilder WithLegacyUri(string uri) => WithLegacyUri(new Uri(uri));
    public ConfigBuilder WithLegacyUri(Uri uri)
    {
        _legacyUri = uri ?? throw new ArgumentNullException(nameof(uri));
        return this;
    }

    /// <summary>Configures the old PHP API key and optional user credentials.</summary>
    public ConfigBuilder WithLegacyAuth(Func<AuthBuilder, Auth> configure)
        => WithLegacyAuth((configure ?? throw new ArgumentNullException(nameof(configure)))(new AuthBuilder()));
    public ConfigBuilder WithLegacyAuth(Auth auth)
    {
        _legacyAuth = auth ?? throw new ArgumentNullException(nameof(auth));
        return this;
    }

    public ConfigBuilder WithSessionLifetime(TimeSpan lifetime) { _sessionLifetime = lifetime; return this; }
    public ConfigBuilder WithClock(Func<DateTimeOffset> clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        return this;
    }

    /// <summary>Validates and creates the configuration.</summary>
    public Config Build()
    {
        if (_uri is null)
            throw new InvalidOperationException("An API URI is required.");
        if (!_uri.IsAbsoluteUri)
            throw new ArgumentException("The API URI must be absolute.", nameof(_uri));
        ValidateOptionalUri(_legacyUri, nameof(_legacyUri));
        if (_sessionLifetime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(_sessionLifetime));
        ValidateOptionalUri(_siteUri, nameof(_siteUri));
        ValidateOptionalUri(_imageServerUri, nameof(_imageServerUri));
        if (_requestTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(_requestTimeout), "The request timeout must be positive.");
        if (_maxRetries < 0)
            throw new ArgumentOutOfRangeException(nameof(_maxRetries), "The retry count cannot be negative.");
        if (_retryDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(_retryDelay), "The retry delay cannot be negative.");
        if (_auth is null)
            throw new InvalidOperationException("Authentication settings are required.");

        return new Config
        {
            Uri = _uri,
            Auth = _auth,
            LegacyAuth = _legacyAuth,
            LegacyUri = _legacyUri,
            SessionLifetime = _sessionLifetime,
            Clock = _clock,
            SiteUri = _siteUri,
            ImageServerUri = _imageServerUri,
            RequestTimeout = _requestTimeout,
            MaxRetries = _maxRetries,
            RetryDelay = _retryDelay,
        };
    }

    private static void ValidateOptionalUri(Uri uri, string name)
    {
        if (uri is not null && !uri.IsAbsoluteUri)
            throw new ArgumentException("The URL must be absolute.", name);
    }
}
