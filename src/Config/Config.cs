using System;
using ShindenAPI.Credentials;

namespace ShindenAPI;

/// <summary>Immutable settings used to create a <see cref="Shinden"/> client.</summary>
public class Config
{
    internal static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan DefaultSessionLifetime = TimeSpan.FromMinutes(90);
    internal static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(250);
    internal static readonly Func<DateTimeOffset> DefaultClock = static () => DateTimeOffset.UtcNow;

    /// <summary>Base URI used for API requests.</summary>
    public required Uri Uri { get; init; }

    /// <summary>Authentication settings used for API requests.</summary>
    public required Auth Auth { get; init; }

    /// <summary>Optional site URI used when generating profile and title links.</summary>
    public Uri SiteUri { get; init; }

    /// <summary>Optional image server URI used when generating image links.</summary>
    public Uri ImageServerUri { get; init; }

    /// <summary>Maximum time allowed for an individual request.</summary>
    public TimeSpan RequestTimeout { get; init; } = DefaultRequestTimeout;

    /// <summary>How long a logged-in user session remains valid.</summary>
    public TimeSpan SessionLifetime { get; init; } = DefaultSessionLifetime;

    /// <summary>Maximum number of retries for transient idempotent requests.</summary>
    public int MaxRetries { get; init; } = 2;

    /// <summary>Base delay between transient request retries.</summary>
    public TimeSpan RetryDelay { get; init; } = DefaultRetryDelay;

    /// <summary>Clock used for session expiration and deterministic testing.</summary>
    public Func<DateTimeOffset> Clock { get; init; } = DefaultClock;
}
