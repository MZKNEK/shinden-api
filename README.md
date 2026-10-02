# Shinden.NET

[![Nuget](https://img.shields.io/nuget/v/Shinden.NET)](https://www.nuget.org/packages/Shinden.NET/) [![codecov](https://img.shields.io/codecov/c/github/MZKNEK/shinden-api?token=KZBU03WLNJ)](https://codecov.io/gh/MZKNEK/shinden-api) [![Build status](https://img.shields.io/appveyor/build/MrZnake/shinden-api)](https://ci.appveyor.com/project/mrznake/shinden-api/branch/master) [![CodeFactor](https://www.codefactor.io/repository/github/mzknek/shinden-api/badge)](https://www.codefactor.io/repository/github/mzknek/shinden-api) [![License](https://img.shields.io/github/license/MZKNEK/shinden-api)](https://github.com/MZKNEK/shinden-api/blob/master/LICENSE)

Asynchronous .NET client for the Shinden API.

## Requirements

- .NET 6, .NET 8, or .NET Standard 2.0
- A Shinden API token
- A descriptive user-agent string

## Installation

```bash
dotnet add package Shinden.NET
```

## Configuration

`ConfigBuilder` requires an absolute API URI and authentication settings. Keep
the token outside source control, for example in an environment variable.

```csharp
using System;
using ShindenAPI;

var shinden = new Shinden(config => config
	.WithUri("https://api.shinden.eu/")
	.WithAuth(auth => auth
		.WithToken(Environment.GetEnvironmentVariable("SHINDEN_API_TOKEN")!)
		.WithUserAgent("MyApp/1.0")
		.Build()));
```

User authentication can be enabled by adding `WithUsername` and
`WithPassword`:

```csharp
using System;

var shinden = new Shinden(config => config
	.WithUri("https://api.shinden.eu/")
	.WithAuth(auth => auth
		.WithToken(Environment.GetEnvironmentVariable("SHINDEN_API_TOKEN")!)
		.WithUsername(Environment.GetEnvironmentVariable("SHINDEN_USERNAME")!)
		.WithPassword(Environment.GetEnvironmentVariable("SHINDEN_PASSWORD")!)
		.WithUserAgent("MyApp/1.0")
		.Build()));
```

Custom site and image hosts can be configured independently from the API host:

```csharp
var shinden = new Shinden(config => config
	.WithUri("https://api.example.test/")
	.WithSiteUri("https://site.example.test/")
	.WithImageServerUri("https://images.example.test/")
	.WithAuth(auth => auth
		.WithToken(Environment.GetEnvironmentVariable("SHINDEN_API_TOKEN")!)
		.WithUserAgent("MyApp/1.0")
		.Build()));
```

Transport behavior can be tuned through `ConfigBuilder`:

```csharp
var shinden = new Shinden(config => config
	.WithUri("https://api.shinden.eu/")
	.WithRequestTimeout(TimeSpan.FromSeconds(20))
	.WithSessionLifetime(TimeSpan.FromMinutes(45))
	.WithMaxRetries(2)
	.WithRetryDelay(TimeSpan.FromMilliseconds(250))
	.WithAuth(auth => auth
		.WithToken(Environment.GetEnvironmentVariable("SHINDEN_API_TOKEN")!)
		.WithUserAgent("MyApp/1.0")
		.Build()));
```

Every outgoing request includes the required `Accept-Language`, `Accept`, and
`User-Agent` headers. If Marmolade authentication is configured, its header is
also added to every request and retry. Automatic retries are limited to
transient failures from `GET`, `HEAD`, and `OPTIONS`; mutation requests are
never retried automatically.

## Domain Clients

The `Shinden` facade exposes focused clients that share one HTTP session:

- `Search` for anime search
- `Titles` for title information, episodes, relations, reviews, and recommendations
- `Users` for user information, lists, ratings, favourites, and login
- `People` for staff and character details
- `Characters` for character collections and flat lists
- `Episodes` for new episodes and the current season
- `Experimental` for bulk character lists and season ID projections

```csharp
var result = await shinden.Search.AnimeAsync(options => options
	.WithQuery("Fullmetal Alchemist")
	.WithOffset(0)
	.WithPageSize(20));

if (!result.IsOk())
	throw new InvalidOperationException(result.GetMessage());

var search = result.Get();
```

The `Shinden` root client only exposes these domain clients (plus `AskAsync`
for custom queries and the static URL helpers); it no longer duplicates every
domain-client method as a root-level convenience method.

## From Beta Versions

The older beta versions in `Shinden.NET` and `shindenapi` used modules on
`ShindenClient`. Their responsibilities are now split into focused clients:

| Beta version | Current version |
| --- | --- |
| `ShindenClient.Search` / `SearchModule` | `Shinden.Search` / `SearchClient` |
| `ShindenClient.Title` / `TitleModule` | `Shinden.Titles` / `TitlesClient` |
| `ShindenClient.User` / `LoggedInUserModule` | `Shinden.Users` / `UsersClient` |
| `ShindenClient.Ex` / `ExperimentalModule` | `Shinden.Experimental` / `ExperimentalClient` |
| Root staff and character methods | `Shinden.People` / `PeopleClient` |
| Root episode and season methods | `Shinden.Episodes` / `EpisodesClient` |

The current version keeps the same API areas while using shared request
management, typed errors, cancellation tokens, and configurable URL resolution.

### Experimental endpoints

`Experimental` contains bulk endpoints carried over from the previous library.
Their response shapes or availability may change:

```csharp
var titleIds = await shinden.Experimental.AnimeFromSeasonAsync();
var characterIds = await shinden.Experimental.AnimeFromSeasonAsync(characters: true);
var allCharacters = await shinden.Experimental.AllCharactersFlatAsync();
```

The `AllCharactersFlatAsync`, `AnimeCharactersFlatAsync`, and
`MangaCharactersFlatAsync` methods intentionally return the raw `CharacterFlat`
DTO from these endpoints. Use `Characters` and `Episodes` for the stable,
domain-oriented response models.

The stable `Characters` and `Episodes` clients expose the underlying endpoints
when a complete response model is preferred.

## Favourites

Favourite requests use `ShindenAPI.Queries.Builder`:

```csharp
using ShindenAPI.Queries.Builder;

var result = await shinden.Users.AddToFavouritesAsync(builder => builder
	.WithUserId(123)
	.WithTitleId(456)
	.Build());
```

## URL Resolution

Parsed user, title, staff, and character models expose resolved URLs such as
`ProfileUrl`, `TitleUrl`, `StaffUrl`, `CharacterUrl`, and `PictureUrl`.

The resolver uses the configured API host for page URLs and HTTPS for CDN
images. `ConfigBuilder.WithSiteUri` and `WithImageServerUri` override those
hosts for a client. For standalone model mapping, construct a
`ShindenUrlResolver` with the corresponding URIs and apply it to models
implementing `IShindenUrlAware`.

Static helpers are also available through `ShindenUrlResolver.Default` and the
matching methods on `Shinden`.

## Error Handling

Requests return `ErrorOr<T>`. Use `IsOk()` and `Get()` for a throwing access, or
`TryGet()` when the failure should be handled explicitly:

```csharp
if (result.TryGet(out var value))
{
	Console.WriteLine(value);
}
else
{
	Console.Error.WriteLine($"{result.GetCode()}: {result.GetMessage()}");
}
```

Cancellation tokens are accepted by all asynchronous operations and are passed
to the underlying `HttpClient` request.

## Development

Build the library for every supported target framework:

```bash
dotnet build src/Shinden.NET.csproj
```

Run the test suite:

```bash
dotnet test test/Shinden.NET.Test.csproj
```

The optional `Shinden.NET.DependencyInjection` package provides
`IHttpClientFactory` registration through `AddShinden(...)`.

The project uses Newtonsoft.Json for response parsing and Microsoft.Extensions.Logging
for optional request logging.
