using System;
using ShindenAPI.Credentials;

namespace ShindenAPI;

/// <summary>Immutable settings used to create a <see cref="Shinden"/> client.</summary>
public class Config
{
    internal static readonly TimeSpan DefaultSessionLifetime = TimeSpan.FromMinutes(90);
    internal static readonly Func<DateTimeOffset> DefaultClock = static () => DateTimeOffset.UtcNow;
    internal static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(250);
    internal static readonly TimeSpan DefaultRateLimitWindow = TimeSpan.FromMinutes(1);

    /// <summary>Base URI used for API requests.</summary>
    public required Uri Uri { get; init; }

    /// <summary>Authentication settings used for API requests.</summary>
    public required Auth Auth { get; init; }

    /// <summary>PHP API credentials, independent of the new API application token.</summary>
    public Auth LegacyAuth { get; init; }
    /// <summary>PHP API origin. Defaults to the new API origin when omitted.</summary>
    public Uri LegacyUri { get; init; }
    public TimeSpan SessionLifetime { get; init; } = DefaultSessionLifetime;
    public Func<DateTimeOffset> Clock { get; init; } = DefaultClock;

    /// <summary>Optional site URI used when generating profile and title links.</summary>
    public Uri SiteUri { get; init; }

    /// <summary>Optional image server URI used when generating image links.</summary>
    public Uri ImageServerUri { get; init; }

    /// <summary>Maximum time allowed for an individual request.</summary>
    public TimeSpan RequestTimeout { get; init; } = DefaultRequestTimeout;

    /// <summary>Maximum number of retries for transient idempotent requests.</summary>
    public int MaxRetries { get; init; } = 2;

    /// <summary>Base delay between transient request retries.</summary>
    public TimeSpan RetryDelay { get; init; } = DefaultRetryDelay;

    /// <summary>
    /// How long a request may wait for a rate-limit pause (after HTTP 429) or a client-side limiter slot.
    /// Zero, the default, returns a 429 error with <see cref="Error.RetryAfter"/> immediately instead of waiting.
    /// </summary>
    public TimeSpan RateLimitMaxWait { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// How many times a GET request is sent again after HTTP 429, once the Retry-After has passed.
    /// Zero by default; requests that change data are never retried after 429.
    /// </summary>
    public int RateLimitRetries { get; init; }

    /// <summary>Client-side limit of requests started within <see cref="RateLimitWindow"/>, per API credentials; zero disables it.</summary>
    public int RateLimitRequests { get; init; }

    /// <summary>Window of the client-side limit set by <see cref="RateLimitRequests"/>.</summary>
    public TimeSpan RateLimitWindow { get; init; } = DefaultRateLimitWindow;
}
