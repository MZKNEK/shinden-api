using System;
using ShindenAPI.Credentials;

namespace ShindenAPI;

/// <summary>Fluent builder for validated <see cref="Config"/> instances.</summary>
public class ConfigBuilder
{
    private Auth _auth;
    private Uri _uri;
    private Uri _siteUri;
    private Uri _imageServerUri;
    private TimeSpan _requestTimeout = Config.DefaultRequestTimeout;
    private TimeSpan _sessionLifetime = Config.DefaultSessionLifetime;
    private int _maxRetries = 2;
    private TimeSpan _retryDelay = Config.DefaultRetryDelay;
    private Func<DateTimeOffset> _clock = Config.DefaultClock;

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

    /// <summary>Sets the lifetime of an authenticated session.</summary>
    public ConfigBuilder WithSessionLifetime(TimeSpan lifetime)
    {
        _sessionLifetime = lifetime;
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

    /// <summary>Sets the clock used for session expiration.</summary>
    public ConfigBuilder WithClock(Func<DateTimeOffset> clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
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

    /// <summary>Validates and creates the configuration.</summary>
    public Config Build()
    {
        if (_uri is null)
            throw new InvalidOperationException("An API URI is required.");
        if (!_uri.IsAbsoluteUri)
            throw new ArgumentException("The API URI must be absolute.", nameof(_uri));
        ValidateOptionalUri(_siteUri, nameof(_siteUri));
        ValidateOptionalUri(_imageServerUri, nameof(_imageServerUri));
        if (_requestTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(_requestTimeout), "The request timeout must be positive.");
        if (_sessionLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(_sessionLifetime), "The session lifetime must be positive.");
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
            SiteUri = _siteUri,
            ImageServerUri = _imageServerUri,
            RequestTimeout = _requestTimeout,
            SessionLifetime = _sessionLifetime,
            MaxRetries = _maxRetries,
            RetryDelay = _retryDelay,
            Clock = _clock
        };
    }

    private static void ValidateOptionalUri(Uri uri, string name)
    {
        if (uri is not null && !uri.IsAbsoluteUri)
            throw new ArgumentException("The URL must be absolute.", name);
    }
}