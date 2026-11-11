# Shinden.NET

[![Nuget](https://img.shields.io/nuget/v/Shinden.NET)](https://www.nuget.org/packages/Shinden.NET/) [![codecov](https://img.shields.io/codecov/c/github/MZKNEK/shinden-api?token=KZBU03WLNJ)](https://codecov.io/gh/MZKNEK/shinden-api) [![Build status](https://img.shields.io/appveyor/build/MrZnake/shinden-api)](https://ci.appveyor.com/project/mrznake/shinden-api/branch/master) [![CodeFactor](https://www.codefactor.io/repository/github/mzknek/shinden-api/badge)](https://www.codefactor.io/repository/github/mzknek/shinden-api) [![License](https://img.shields.io/github/license/MZKNEK/shinden-api)](https://github.com/MZKNEK/shinden-api/blob/master/LICENSE)

Asynchronous .NET client for the Shinden Go API and the retained PHP API.
Version 0.7 supports .NET 6, .NET 8 and .NET Standard 2.0.

Go is the preferred implementation. Once a function has a Go route, the
client uses that route exclusively. Legacy methods remain temporarily for
functions that have no Go counterpart yet. A compatibility response format
or an `/api` prefix does not imply that the server still runs PHP.

## Configuration

```csharp
using System;
using ShindenAPI;

using var shinden = new Shinden(config => config
    .WithUri("https://shinden.pl/")
    .WithAuth(auth => auth
        .WithToken(Environment.GetEnvironmentVariable("SHINDEN_API_TOKEN"))
        .WithUserAgent("MyApp/1.0")
        .Build())
    .Build());
```

Use `https://shinden.pl/` as the base URI, without `/api`, `/v1` or
`/compat/php-api`. A custom proxy base path is preserved, with or without
a trailing slash.

New API application tokens (`shk_…`) are sent as `Authorization: Bearer …`
on each new API request and retry, never as a URL parameter.
Omit `WithToken` to use public endpoints as a guest.
The user agent is required. Application tokens grant no user login rights;
their modules must cover the endpoints being called (for example
`compat/php-api`, `search`, `users`, `catalog`).

PHP operations that have not moved to the new API continue to use `/api/…`
with the old API key. Configure that key independently with `WithLegacyAuth`:

```csharp
using var shinden = new Shinden(config => config
    .WithUri("https://shinden.pl/")
    .WithAuth(auth => auth
        .WithToken(Environment.GetEnvironmentVariable("SHINDEN_API_TOKEN"))
        .WithUserAgent("MyApp/1.0")
        .Build())
    .WithLegacyAuth(auth => auth
        .WithToken(Environment.GetEnvironmentVariable("SHINDEN_LEGACY_API_KEY"))
        .WithUsername(Environment.GetEnvironmentVariable("SHINDEN_USERNAME"))
        .WithPassword(Environment.GetEnvironmentVariable("SHINDEN_PASSWORD"))
        .WithUserAgent("MyApp/1.0")
        .Build())
    .Build());
```

PHP requests send the old key as `api_key`, as before. Username and password
are optional for PHP reads; configure both for operations that require a
logged-in user. Login is automatic before PHP requests when user credentials
are configured, and the session is reused until it expires. `WithMarmolade`,
`WithSessionLifetime` and `WithClock` remain available for PHP authentication
and session handling.

Login cookies are read from `Set-Cookie`, so renamed session cookies are
preserved. Older login responses without cookie headers fall back to the
historical `name` and `id` JSON session values. These cookies remain isolated
from independent application-token requests.

`WithLegacyUri` optionally sets a separate PHP API origin; it defaults to
`WithUri`. Both an origin and a historical address ending in `/api` are
accepted for `WithLegacyUri`. The old key and session cookies are used only
for PHP requests. New API calls do not trigger PHP login. A missing old key
returns a configuration error for PHP operations; the new token is never
substituted for it, and failed writes are never redirected to another API.

Historical configurations using `UserAuth` through `WithAuth` or the
`Shinden(Uri, Auth)` constructor still support PHP login and operations.
In this configuration, new API requests run as a guest. To supply both keys,
use separate `WithAuth` and `WithLegacyAuth` settings as shown above.

### Changing the configuration at runtime

`Reconfigure` changes the settings of an existing client, without creating a
new `Shinden` object (useful with a singleton registered through DI). The
builder starts from the current configuration, so set only what changes:

```csharp
shinden.Reconfigure(config => config
    .WithAuth(auth => auth
        .WithToken(newToken)
        .WithUserAgent("MyApp/1.0")
        .Build())
    .WithMaxRetries(5)
    .Build());

var current = shinden.Config;
```

A complete `Config` can also be passed with `Reconfigure(config)`. The new
configuration is validated first; if it is invalid, an exception is thrown and
nothing changes. Requests already in progress finish with the old settings.
The PHP session is kept unless the PHP credentials, key or address change; then
the next PHP request logs in again. The HTTP client, handler and logger passed
to the constructor stay the same.

## Clients

| Client | Operations |
| --- | --- |
| `Search` | Title, user, character, staff and tag suggestions; titles by IDs; anime/manga browse; seasons |
| `Titles` | Compatibility title info, episodes and characters; typed v1 anime card; PHP episode range, recommendations, reviews and relations |
| `Users` | Compatibility profile and watch/read history; typed v1 statistics; PHP login, favourites, list updates and ratings |
| `Characters` | Compatibility character details and flat lists; typed v1 card; PHP character ID lists and manga flat list |
| `People` | PHP staff details |
| `Episodes` | Recent online episodes and current-season IDs |
| `PocketWaifu` | Public card copy: galleries, IDs, tags, genres and individual cards |

`HealthAsync` calls `/health`. `AskAsync` accepts typed queries from
`ShindenAPI.Queries`, including optional compatibility lookup and language
parameters. Custom GET requests can derive from `GetRequest<T>`.
Legacy query classes are also accepted by `AskAsync` and are routed to PHP.

The methods retained on PHP are:

- `Users.LoginAsync`, `LoginResultAsync`, `IsValidSession`, `FavouriteCharactersAsync`,
  `AddToFavouritesAsync`, `RemoveFromFavouritesAsync`, `ChangeTitleStatusAsync`,
  `RemoveTitleStatusAsync`, `IncreaseWatchedEpisodeAsync`, `RateAnimeAsync`, `RateMangaAsync`.
- `Titles.EpisodeRangeAsync`, `RecommendationsAsync`, `ReviewsAsync`, `RelationsAsync`.
- `People.StaffInfoAsync`.
- `Characters.ConnectedIdsAsync`, `AnimeIdsAsync`, `MangaIdsAsync`, `MangaFlatListAsync`.

These methods retain their existing signatures and request payloads. Their
migration to the new API can happen separately when replacement routes become
available. POST and DELETE operations are never retried automatically.

## Search

```csharp
var suggestions = await shinden.Search.TitlesAsync("Gintama");
var users = await shinden.Search.UsersAsync("Sniku", limit: 20);
var browse = await shinden.Search.AnimeAsync(options => options
    .WithQuery("Fullmetal Alchemist")
    .WithPage(1)
    .WithPageSize(20)
    .WithType("TV")
    .WithSort("title")
    .Build());

var season = await shinden.Search.SeasonAsync(
    2026, ShindenAPI.Queries.V1.Season.Fall);
```

Browse now uses GET query parameters: `page` (starting at 1), `limit` and PHP
arrays such as `series_type[]`. `SearchResult.Total` maps to `total`.
`MangaAsync` accepts the same options object. `With` and `WithIndexed` allow
additional browse filters supported by the server.

Suggestion limits are 20 for users, characters, staff and tags, and 50 for
titles. Title suggestions support `ignoreId` and include Webtoon/Webnovel
in manga searches. Titles by IDs accept 1–100 positive IDs. Public search
has a shared IP limit of 120 requests per minute;
an application token does not raise that limit.

## Profiles, catalog and compatibility data

```csharp
var profile = await shinden.Users.InfoAsync(12345);
var stats = await shinden.Users.StatsAsync(12345);
var anime = await shinden.Titles.AnimeAsync(11);
var character = await shinden.Characters.CatalogAsync(1000);
var history = await shinden.Users.LastWatchedAsync(12345, limit: 100);

var animeRelations = await shinden.AskAsync(
    new ShindenAPI.Queries.People.CharacterInfo(1000,
        new ShindenAPI.Queries.People.CharacterInfoOptions { AnimeOnly = true }));
```

Compatibility routes use `/compat/php-api/…`. Their database values may be
strings or null. `PhpRow.AdditionalData` preserves undocumented columns.
Profile statistics are top-level fields (`WatchedStatus`, `WatchTime`,
`MeanAnimeScore`, etc.); use `StatsAsync` for the new typed response.
Email, birthdate and sex are private to the profile owner, so an application
token alone does not expose them. History limits are 1–100.

For non-anime titles, the compatibility episode endpoint returns an empty
successful body; the client exposes empty episode lists. A missing history
or other resource remains an HTTP 404 error, including when its body is empty.
`CharacterFlat.RankingPosition` represents the `n` field; it is not a title ID.

## Pocket Waifu

```csharp
using ShindenAPI.Queries.V1;

var result = await shinden.PocketWaifu.CardsAsync(12345, new PwCardOptions
{
    Rarity = "SSS,SS",
    Tradable = true,
    Limit = 100,
    Sort = "power",
    Direction = "desc"
});

if (result.TryGet(out var page) && page.NextCursor is not null)
{
    var nextPage = await shinden.PocketWaifu.CardsAsync(12345, new PwCardOptions
    {
        Rarity = "SSS,SS", Tradable = true, Limit = 100,
        Sort = "power", Direction = "desc", Cursor = page.NextCursor
    });
}
```

Page sizes are 48, 100 or 200. Preserve the filters and pass `NextCursor`
unchanged to fetch the next page. `CardIdsAsync` accepts the same filters
and returns the matching IDs without pagination. Check `Status` (`ok`,
`none`, `pending`, `disabled`) and `Truncated` on the appropriate responses.

## Errors and transport

All operations return `ErrorOr<T>` and accept cancellation tokens.

```csharp
var result = await shinden.Users.StatsAsync(12345);
if (result.TryGet(out var value))
    Console.WriteLine(value.Anime.EpisodesWatched);
else
    Console.Error.WriteLine($"{result.Error.Code}: {result.Error.ApiCode}: {result.Error.Message}");
```

`Error.Code` is the numeric HTTP status for HTTP failures; local failures
use a synthetic code (for example, `500` for parsing and `408` for timeout).
`ApiCode`, `Field`, `RetryAfter` and `RetryAfterSeconds` expose v1 error details;
`RawResponse` retains HTTP error bodies.
Compatibility errors use their `status` as the message. Non-JSON and empty
error bodies are supported. Caller cancellation propagates; request timeouts
produce `ErrorType.Timeout` and invalid JSON produces `ErrorType.Parsing`.
For exception-based failures, `ExceptionType` identifies the original type.
JSON failures also expose `JsonPath`, `JsonLineNumber` and `JsonLinePosition`
when available. Required identity fields in profiles, search results,
title details, v1 catalog cards and statistics are checked during deserialization;
missing or null identities produce parsing errors. Optional fields and
additional server fields remain supported.

Pass an `ILogger<Shinden>` to the constructor to enable runtime diagnostics:

```csharp
// loggerFactory is the application's configured Microsoft.Extensions.Logging factory.
using var shinden = new Shinden(config => config
    .WithUri("https://shinden.pl/")
    .WithAuth(auth => auth
        .WithToken(Environment.GetEnvironmentVariable("SHINDEN_API_TOKEN"))
        .WithUserAgent("MyApp/1.0")
        .Build())
    .Build(), logger: loggerFactory.CreateLogger<Shinden>());
```

The DI package resolves `ILogger<Shinden>` from the service container.
Configure a logging provider in the application and enable category
`ShindenAPI.Shinden` at `Warning` or a more verbose level. Without a logger,
errors remain available through `ErrorOr<T>`.

HTTP, parsing, request construction, authentication and transport failures
are logged at `Error`; timeouts at `Warning`; successful HTTP responses at
`Information`; request/parsing progress at `Trace`. Caller cancellation is
propagated without logging it as a timeout. Parsing logs contain the method,
endpoint path, HTTP status, query/model types and JSON location. They include
an `exceptionType` structured field and a sanitized exception snapshot that
keeps the original type names, stack traces and inner exception diagnostics.
Configured keys, passwords and known session secrets are masked throughout
the log state and exception snapshots. Request URLs are logged as paths
without their query strings; request bodies and exception `Data` are not
attached to logs. `Error.Message` and `RawResponse` retain
original diagnostic data and do not use the logger's redaction.

Configure `WithRequestTimeout`, `WithMaxRetries` and `WithRetryDelay` as
needed. Defaults are 30 seconds, two retries and a 250 ms base delay.
Transient GET failures (408 and 5xx) are retried, honoring `Retry-After`; a server delay
that exceeds the remaining request timeout returns the HTTP error immediately.
New API requests do not use PHP user sessions or cookies.

### Rate limits

The API limits requests per token and answers `429 Too Many Requests` with a
`Retry-After` header. `Error.RetryAfter` (`TimeSpan?`) carries that wait for any
error status, read from the header (seconds or an HTTP date) or from the v1
`retry_after` field; `RetryAfterSeconds` is the same value rounded up.

After a 429 with a wait time, the client pauses: until it passes, requests with
the same credentials are not sent and return at once a `429` error with
`ErrorType.RateLimit` and the time left in `RetryAfter`. The new API and the PHP
API are paused separately, as they use different credentials, and changing the
token or address through `Reconfigure` ends the pause. Only 429 pauses the
client; 467, 503 and other statuses do not. The pause start (`Warning`) and end
(`Information`) are logged with their duration.

```csharp
using var shinden = new Shinden(config => config
    .WithUri("https://shinden.pl/")
    .WithAuth(auth => auth.WithToken(token).WithUserAgent("MyBot/1.0").Build())
    // Wait up to 10 s for a pause or a limiter slot instead of failing at once.
    .WithRateLimitMaxWait(TimeSpan.FromSeconds(10))
    // Send a GET once more after 429, when Retry-After has passed.
    .WithRateLimitRetries(1)
    // Start at most 290 requests per minute, below the server's 300.
    .WithRateLimit(290, TimeSpan.FromMinutes(1))
    .Build());
```

All three are off by default. `WithRateLimitMaxWait` bounds each wait — before
the first attempt and before each retry — and callers can still cancel it; waits
do not count towards the request timeout, which applies to every attempt.
`TimeSpan.MaxValue` waits as long as possible (about 24 days). `WithRateLimitRetries` applies only to GET, HEAD and OPTIONS — requests
that change data are never sent again after 429. Its wait is bounded by
`WithRateLimitMaxWait`, or by the request timeout when no maximum wait is set.
The client-side limit counts each call once (transient retries and redirects are
not counted, so leave some headroom) and applies to each API's credentials
separately. When no slot frees up within the maximum wait, the call returns a
`429` error with `ErrorType.RateLimit` without being sent.

The pause and the limit belong to one `Shinden` instance. Share a single instance
per token — the dependency injection package registers it as a singleton by
default; `Scoped` or `Transient` lifetimes give every instance its own pause and limit.

Redirects (301, 302, 303, 307, 308) are followed by the library, up to 10 per
request and within the same timeout. The Bearer token, old-API Marmolade header
and PHP session cookies are sent only while every hop stays on the original
host; `www.` prefixes and HTTP-to-HTTPS upgrades count as the same host. Other
hosts receive only `Accept`, `Accept-Language` and `User-Agent`. HTTPS-to-HTTP
redirects are not followed and return the redirect status as an error.
307 and 308 keep the method and body; 303, and POST after 301 or 302,
continue as a GET without a body. Session cookies set on redirect responses
during PHP login are kept.

`WithSiteUri` and `WithImageServerUri` override generated link hosts. Title
suggestions retain the server's `title_url`; v1 catalog cards contain their
own URLs. Search models expose numeric image IDs and resolved picture URLs.
`UserSearchResult.HasAvatar` maps the new boolean avatar flag.

The optional `Shinden.NET.DependencyInjection` package registers a factory-backed
client using `services.AddShinden(config => …)`. Supplied `HttpClient` and
`HttpMessageHandler` instances remain owned by the caller, and default client
headers are not modified.
For a caller-supplied transport used with both APIs, disable automatic cookie
handling (`HttpClientHandler.UseCookies = false`) so PHP response cookies cannot
be added to new API requests by the handler. The built-in and dependency injection
transports already disable it; PHP session cookies are attached explicitly by the library.
Likewise, set `HttpClientHandler.AllowAutoRedirect = false` on a supplied transport
so the library's redirect handling applies; the built-in transports already do.

## Development

Manual tests against a real server are available in [live/README.md](live/README.md).
Fill `live/live-settings.local.json` with the separate new API and PHP credentials.
The live runner requires an explicit `--run-live` command, is blocked in CI,
and is not executed by `dotnet test`. Writes are disabled by default.

```bash
dotnet build src/Shinden.NET.csproj
dotnet build src/DependencyInjection/Shinden.NET.DependencyInjection.csproj
dotnet test test/Shinden.NET.Test.csproj
```

Tests run offline against local handlers and verify request routes and response
models for compatibility, v1 and PHP endpoints, plus PHP session handling,
credential isolation and mutation retry behavior. The library uses Newtonsoft.Json
and optional Microsoft.Extensions.Logging.
