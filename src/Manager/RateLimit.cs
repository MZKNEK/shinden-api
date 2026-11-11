using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace ShindenAPI;

/// <summary>Rate-limit settings taken from <see cref="Config"/>; validated so a direct <see cref="Config"/> cannot bypass the builder.</summary>
internal sealed record RateLimitOptions
{
    internal static readonly RateLimitOptions Default = new(TimeSpan.Zero, 0, 0, Config.DefaultRateLimitWindow);

    internal RateLimitOptions(TimeSpan maxWait, int retries, int requests, TimeSpan window)
    {
        if (maxWait < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maxWait), "The rate-limit wait cannot be negative.");
        if (retries < 0)
            throw new ArgumentOutOfRangeException(nameof(retries), "The rate-limit retry count cannot be negative.");
        if (requests < 0)
            throw new ArgumentOutOfRangeException(nameof(requests), "The request limit cannot be negative.");
        if (requests > 0 && window <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(window), "The rate-limit window must be positive.");

        MaxWait = maxWait;
        Retries = retries;
        Requests = requests;
        Window = window;
    }

    internal static RateLimitOptions From(Config config)
        => new(config.RateLimitMaxWait, config.RateLimitRetries, config.RateLimitRequests, config.RateLimitWindow);

    internal TimeSpan MaxWait { get; }
    internal int Retries { get; }
    internal int Requests { get; }
    internal TimeSpan Window { get; }
}

/// <summary>Monotonic time, unaffected by changes of the system clock.</summary>
internal static class MonotonicClock
{
    private static readonly long Start = Stopwatch.GetTimestamp();

    internal static TimeSpan Now
        => TimeSpan.FromTicks((long)((Stopwatch.GetTimestamp() - Start) * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency)));
}

/// <summary>
/// The pause started by an HTTP 429 response: until its Retry-After passes, no request is sent with the same credentials.
/// Logs when the pause starts and when it ends.
/// </summary>
internal sealed class RateLimitBlock : IDisposable
{
    // Timer due times are capped; a longer pause is simply rescheduled when the timer fires.
    private static readonly TimeSpan MaxTimerDelay = TimeSpan.FromHours(1);

    private readonly object _lock = new();
    private readonly ILogger _logger;
    private readonly string _api;
    private Timer _timer;
    private TimeSpan _until;
    private TimeSpan _started;
    private bool _active;
    private bool _disposed;

    internal RateLimitBlock(ILogger logger, string api)
    {
        _logger = logger;
        _api = api;
    }

    /// <summary>Time left until requests may be sent again; zero when not paused.</summary>
    internal TimeSpan Remaining
    {
        get
        {
            lock (_lock)
            {
                var remaining = _until - MonotonicClock.Now;
                return _active && remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }
    }

    /// <summary>Pauses requests for <paramref name="delay"/>; an existing longer pause is kept.</summary>
    internal void Pause(TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
            return;

        bool started;
        lock (_lock)
        {
            if (_disposed)
                return;
            var now = MonotonicClock.Now;
            var until = now + delay;
            if (_active && until <= _until)
                return;

            started = !_active;
            if (started)
                _started = now;
            _active = true;
            _until = until;
            _timer ??= new Timer(static block => ((RateLimitBlock)block).OnTimer(), this, Timeout.Infinite, Timeout.Infinite);
            _timer.Change(Min(delay, MaxTimerDelay), Timeout.InfiniteTimeSpan);
        }

        if (started)
            Log(LogLevel.Warning, "[rate limit]: {api} API returned 429; requests are paused for {seconds} s, until {until}",
                _api, Seconds(delay), DateTimeOffset.UtcNow + delay);
        else
            Log(LogLevel.Debug, "[rate limit]: {api} API pause extended by another 429; {seconds} s left",
                _api, Seconds(delay));
    }

    private void OnTimer()
    {
        TimeSpan duration;
        lock (_lock)
        {
            if (_disposed || !_active)
                return;
            var now = MonotonicClock.Now;
            var remaining = _until - now;
            if (remaining > TimeSpan.Zero)
            {
                _timer.Change(Min(remaining, MaxTimerDelay), Timeout.InfiniteTimeSpan);
                return;
            }
            _active = false;
            duration = now - _started;
        }

        Log(LogLevel.Information, "[rate limit]: {api} API pause ended after {seconds} s; requests are sent again",
            _api, Seconds(duration));
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            _active = false;
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void Log(LogLevel level, string message, params object[] args)
    {
        if (_logger is not null && _logger.IsEnabled(level))
            _logger.Log(level, message, args);
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left < right ? left : right;

    private static double Seconds(TimeSpan value) => Math.Round(value.TotalSeconds, 1);
}

/// <summary>
/// Client-side sliding-window limiter: at most <c>Requests</c> requests start within any <c>Window</c>.
/// The ring keeps the start times of the last <c>Requests</c> reservations, oldest first.
/// </summary>
internal sealed class ClientRateLimiter
{
    private readonly object _lock = new();
    private readonly TimeSpan[] _starts;
    private readonly TimeSpan _window;
    private int _oldest;

    internal ClientRateLimiter(int requests, TimeSpan window)
    {
        _window = window;
        _starts = new TimeSpan[requests];
        for (var i = 0; i < _starts.Length; i++)
            _starts[i] = -window;
    }

    /// <summary>
    /// Reserves the earliest free slot if it starts within <paramref name="maxDelay"/>. <paramref name="delay"/> is the
    /// time until that slot; when the slot is too far away nothing is reserved.
    /// </summary>
    internal bool TryReserve(TimeSpan maxDelay, out TimeSpan delay)
    {
        lock (_lock)
        {
            var now = MonotonicClock.Now;
            var slot = _starts[_oldest] + _window;
            if (slot < now)
                slot = now;
            delay = slot - now;
            if (delay > maxDelay)
                return false;

            _starts[_oldest] = slot;
            _oldest = (_oldest + 1) % _starts.Length;
            return true;
        }
    }
}
