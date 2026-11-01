# Manual live tests

.NET SDK 8 is required. The runner calls the real API through Shinden.NET,
checks deserialization and basic response values, and writes a JSON report.
It is not a test project and does not run through a regular `dotnet test`
command. Live execution is also blocked when a CI environment is detected.

Every live request includes `Cookie: proba=1`, including guest reads, PHP
login, writes and restoration requests. The runner adds this cookie
automatically and preserves PHP session cookies in the same header.
Every request also includes `Accept: application/json`, including legacy
reads, login, writes and restoration.

## Configuration

Fill in `live/live-settings.local.json`. This file and the `live/reports/`
directory are excluded from Git. The included `live-settings.example.json`
is an empty template. After cloning the repository, create a local copy:

```powershell
Copy-Item live/live-settings.example.json live/live-settings.local.json
```

Do not overwrite an existing local file that already contains your settings.

| Field | Value |
| --- | --- |
| `newApi.baseUri` | New API address, defaulting to `https://shinden.pl/`, without `/v1` or `/compat/php-api` |
| `newApi.authentication.bearerToken` | New `shk_…` application token, sent as `Authorization: Bearer …` |
| `legacyApi.baseUri` | Old PHP API address; it may end with `/api` |
| `legacyApi.authentication.apiKey` | Old PHP key, sent as the `api_key` query parameter |
| `legacyApi.authentication.marmoladeHeaderName` | Optional Marmolade header name; leave empty if unused |
| `legacyApi.userAuthentication.enabled` | Set to `true` to test PHP login and session handling |
| `legacyApi.userAuthentication.username` / `password` | PHP account credentials |
| `legacyApi.userAuthentication.expectedUserId` | Optional expected user ID; `0` disables this additional identity check for read-only tests |

The default run requires only the two API keys. User login is disabled by
default. Disable new token tests with `newApi.authenticatedTestsEnabled=false`
or PHP tests with `legacyApi.enabled=false`. Public search and other public
reads will still be tested as a guest. The application token must have access
to every module being tested.

Leave IDs in `data` as `0` to discover them from search results. Set explicit
IDs to test a particular user, anime, manga, character or staff member. For
statistics, choose a user with a public list. For recommendations and reviews,
choose an anime that has them. Use `data.pocketWaifuUserId` to select a player
with cards and `data.cardId` to select a specific card; at `0`, the runner will
try to discover them automatically. Change the year and season through
`data.seasonYear` and `data.season`.

## Running the tests

Run these commands from the repository root.

First, validate the configuration without making network requests:

```powershell
dotnet run --project live/Shinden.NET.LiveTests.csproj -c Release -- --validate --config live/live-settings.local.json
```

Then run the read checks and any enabled PHP login checks:

```powershell
dotnet run --project live/Shinden.NET.LiveTests.csproj -c Release -- --run-live --config live/live-settings.local.json
```

Requests run sequentially, with a default interval of 750 ms and a timeout of
60 seconds per request. Automatic retries are disabled. Adjust these settings
with `requestSpacingMilliseconds` and `requestTimeoutSeconds`.

## Coverage and reports

- Title, user, character, staff and tag search; titles by ID; anime/manga
  browsing; seasons; catalog; statistics; and health.
- Profiles, history, characters, titles, episodes and the current season
  through the compatibility API.
- Pocket Waifu dictionaries, cards, next page, IDs, tags and card details.
  Disable these checks with `includePocketWaifu=false`.
- PHP favourite characters, episode ranges, recommendations, reviews,
  relations and staff details.
- Bulk character and ID lists: set `includeBulkCharacterLists=true` to test
  these large responses. They are skipped by default. For large lists,
  consider increasing `requestTimeoutSeconds` to 120 or more.
- PHP login and a read using its session, when
  `legacyApi.userAuthentication.enabled` is enabled.

Reports are written to `live/reports/live-*.json`. Results use these labels:

- `PASS`: the call and the checked response values are valid.
- `FAIL`: an HTTP error, deserialization error or invalid response data.
- `SKIP`: a disabled check or missing data, such as an empty history, no card
  or no next page. A skipped check does not verify that the route works.

An HTTP 404 with an empty body for history or read-only favourite character
checks is marked as `SKIP`. The character favourite write scenario checks
the list after adding a character, so an empty 404 at that point is a failure.
After removing the character, an empty 404 confirms its absence.
Other unexpected 404 responses, token errors and rate limits remain failures. The report records
the HTTP error status, error type, API code and JSON field path for parsing
errors when available. It does not record configuration, keys, passwords,
cookies or raw responses. Review the report before sharing it; do not share
the configuration file.

Exit codes: `0` means no failures among the checks that ran, `1` means a check
failed, `2` means a configuration error or CI block, and `130` means execution
was cancelled with Ctrl+C. Exit code `0` can include `SKIP` results; review
the coverage summary.

## Optional write tests

Use a test account. These scenarios modify user data in the real PHP API.
All of the following are required:

1. PHP login enabled with valid credentials.
2. `writes.enabled=true` and `writes.expectedUserId` identifying that account.
3. At least one scenario enabled and configured in the `writes` section.
4. The `--allow-writes` command-line flag.

```powershell
dotnet run --project live/Shinden.NET.LiveTests.csproj -c Release -- --run-live --allow-writes --config live/live-settings.local.json
```

Before the first write, the runner compares the logged-in account ID with
`writes.expectedUserId`. IDs discovered through search are not used to select
objects modified by write scenarios.

| Scenario | Required settings and restoration |
| --- | --- |
| `favourite` | `type`: `character`, `title` or `staff`; `objectId`; `initiallyFavourite`: `true` or `false`. For characters, the runner confirms the initial state, adds the character, verifies both list reads, removes it and verifies its absence. Other types are toggled and restored. |
| `titleStatus` | `titleId`, `testStatus`, `initialStatus` and `initialStateConfirmed=true`. `initialStatus=null` means the title is absent from the list. The runner sets a status, removes the entry and attempts to restore the previous state. |
| `animeRating` | `titleId`, `category`, `testValue` and `restoreValue`. Choose a title that already has a rating; both values must be between 1 and 10. |
| `mangaRating` | The same settings as anime ratings. Manga categories: `total`, `story`, `titlecahracters`, `lines`. |
| `increaseWatched` | `titleId`. Permanently increases the watched episode count; this operation has no automatic undo. |

List statuses: `in progress`, `completed`, `dropped`, `hold`, `plan`, `skip`.
Anime rating categories: `total`, `story`, `titlecahracters`, `graphics`, `music`.

For character favourites, `php.users.favourite-characters` and
`php.session.read-favourites` run after the add request. Both must contain
the configured character; the first uses the application key, and the
second also uses the logged-in session. Their `.after-remove` checks must
confirm that the character is absent. If it was already a favourite, the
runner removes it before the add/read/remove sequence, then restores it
and verifies both reads again with `.restored` checks. A mismatch with
`initiallyFavourite` prevents favourite writes. Other favourites are left
in place.

Enter previous values that match the account's actual state. Restoration is
attempted after failures and Ctrl+C as well, with its own timeout. It is not
a transaction and cannot guarantee that additional effects, such as dates or
activity history, are undone. If restoration fails, the report marks the
`cleanup.*` check as `FAIL` and includes details for manual verification.
Forcibly terminating the process prevents restoration from running.

## Testing the runner itself

The regular test suite exercises the runner with fake HTTP responses and no
server connections. It checks configuration, credential separation, read
coverage, report privacy, account identity checks, and restoration after
failures or cancellation.

```powershell
dotnet test test/Shinden.NET.Test.csproj -c Release
```
