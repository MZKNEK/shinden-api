using System.Diagnostics;
using System.Text.RegularExpressions;
using ShindenAPI.Models;
using ShindenAPI.Queries.Builder;
using ShindenAPI.Queries.Search;
using ShindenAPI.Queries.V1;

namespace ShindenAPI.LiveTests;

internal sealed class CheckFailure : Exception
{
    internal CheckFailure(string message) : base(message) { }
}

internal sealed class CheckResult
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public long DurationMilliseconds { get; set; }
    public int? HttpStatus { get; set; }
    public string? ErrorType { get; set; }
    public string? ApiCode { get; set; }
    public string? JsonPath { get; set; }
    public string? Details { get; set; }
}

internal sealed class LiveReport
{
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public bool Cancelled { get; set; }
    public List<CheckResult> Checks { get; } = new();
    public int Passed => Checks.Count(c => c.Status == "PASS");
    public int Failed => Checks.Count(c => c.Status == "FAIL");
    public int Skipped => Checks.Count(c => c.Status == "SKIP");
}

internal sealed class LiveRunner
{
    private readonly LiveSettings _settings;
    private readonly TextWriter _output;
    private readonly HttpMessageHandler? _handler;
    private readonly Func<CancellationToken, Task> _spacing;
    private readonly string? _only;
    private CancellationToken _cancellation;
    internal LiveReport Report { get; } = new();
    private bool DeferFavouriteReads => _settings.Writes.Enabled && _settings.Writes.Favourite.Enabled
        && _settings.Writes.Favourite.Type == "character" && _settings.LegacyApi.UserAuthentication.Enabled;

    internal LiveRunner(LiveSettings settings, TextWriter output, HttpMessageHandler? handler = null,
        Func<CancellationToken, Task>? spacing = null, string? only = null)
    {
        _only = only;
        _settings = settings;
        _output = output;
        _handler = handler;
        _spacing = spacing ?? (token => Task.Delay(settings.RequestSpacingMilliseconds, token));
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        _cancellation = cancellationToken;
        using var transport = new LiveCookieHandler(_handler);
        using var guest = _settings.CreateClient(guest: true, handler: transport);
        using var api = _settings.CreateClient(handler: transport);
        try
        {
            var ids = await ReadChecksAsync(guest, api);
            await LegacyChecksAsync(api, ids);
            if (_settings.LegacyApi.UserAuthentication.Enabled)
            {
                using var userApi = _settings.CreateClient(login: true, handler: transport);
                var user = await Check("php.login", token => userApi.Users.LoginResultAsync(token), value =>
                {
                    Need(value.Id > 0 && userApi.Users.IsValidSession(), "No valid session after login.");
                    var expected = _settings.LegacyApi.UserAuthentication.ExpectedUserId;
                    Need(expected == 0 || value.Id == expected, "The logged-in account does not match legacyApi.userAuthentication.expectedUserId.");
                });
                if (!DeferFavouriteReads)
                    await Check("php.session.read-favourites", token => userApi.Users.FavouriteCharactersAsync(user!.Id, token),
                        value => Need(value is not null, "The favourites list is missing."), enabled: user is not null, skip: "PHP login failed.",
                        allowEmpty404: true, empty404Details: "HTTP 404: the favourite character list is empty; its contents were not verified.");
                await Check("new.after-php-login", token => api.Search.UsersAsync(_settings.Data.UserQuery, cancellationToken: token), NonEmpty);
                if (_settings.Writes.Enabled && user is not null)
                {
                    if (user.Id != _settings.Writes.ExpectedUserId)
                    {
                        Add(new CheckResult { Name = "writes.identity", Status = "FAIL", Details = "The logged-in account does not match writes.expectedUserId. No write requests were sent." });
                        SkipWrites("Test account identity mismatch.");
                    }
                    else await WriteChecksAsync(userApi, api, user.Id);
                }
                else SkipWrites(user is null ? "PHP login failed." : "writes.enabled=false.");
            }
            else
            {
                Skip("php.login", "legacyApi.userAuthentication.enabled=false.");
                Skip("php.session.read-favourites", "PHP login is disabled.");
                SkipWrites("writes.enabled=false; PHP login is disabled.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Report.Cancelled = true;
            throw;
        }
        finally { Report.FinishedAt = DateTimeOffset.UtcNow; }
    }

    private sealed record Resources(ulong User, ulong Anime, ulong Manga, ulong Character, ulong Staff);

    private async Task<Resources> ReadChecksAsync(Shinden guest, Shinden api)
    {
        var data = _settings.Data;
        var authenticated = _settings.NewApi.AuthenticatedTestsEnabled;
        await Check("guest.health", token => guest.HealthAsync(token));
        var users = await Check("guest.search.users", token => guest.Search.UsersAsync(data.UserQuery, cancellationToken: token), NonEmpty);
        var anime = await Check("guest.search.titles.anime", token => guest.Search.TitlesAsync(data.AnimeQuery, TitleQuick.SearchType.Anime, cancellationToken: token), NonEmpty);
        var manga = await Check("guest.search.titles.manga", token => guest.Search.TitlesAsync(data.MangaQuery, TitleQuick.SearchType.Manga, cancellationToken: token), NonEmpty);
        await Check("guest.search.titles.both", token => guest.Search.TitlesAsync(data.AnimeQuery, cancellationToken: token), NonEmpty);
        var characters = await Check("guest.search.characters", token => guest.Search.CharactersAsync(data.CharacterQuery, cancellationToken: token), NonEmpty);
        var staff = await Check("guest.search.staff", token => guest.Search.StaffAsync(data.StaffQuery, cancellationToken: token), NonEmpty);
        await Check("guest.search.tags", token => guest.Search.TagsAsync(data.TagQuery, cancellationToken: token), NonEmpty);
        var ids = new Resources(data.UserId != 0 ? data.UserId : users?.FirstOrDefault()?.Id ?? 0,
            data.AnimeId != 0 ? data.AnimeId : anime?.FirstOrDefault()?.Id ?? 0,
            data.MangaId != 0 ? data.MangaId : manga?.FirstOrDefault()?.Id ?? 0,
            data.CharacterId != 0 ? data.CharacterId : characters?.FirstOrDefault()?.Id ?? 0,
            data.StaffId != 0 ? data.StaffId : staff?.FirstOrDefault()?.Id ?? 0);
        await Check("new.bearer.search", token => api.Search.UsersAsync(data.UserQuery, cancellationToken: token), NonEmpty,
            enabled: authenticated, skip: "New API token tests are disabled.");
        var titleIds = new[] { ids.Anime, ids.Manga }.Where(id => id > 0).Distinct().ToArray();
        await Check("new.search.titles-by-ids", token => api.Search.TitlesByIdsAsync(titleIds, token), values =>
        {
            NonEmpty(values);
            Need(titleIds.All(id => values.Any(value => value.Id == id)), "A title found through search is missing from the by-ids result.");
        }, enabled: titleIds.Length > 0, skip: "No title IDs are available.");
        var browse = new AnimeSearchOptionsBuilder().WithPage(1).WithPageSize(5).Build();
        await Check("new.browse.anime", token => api.Search.AnimeAsync(browse, token), ListResult);
        await Check("new.browse.manga", token => api.Search.MangaAsync(browse, token), ListResult);
        await Check("new.season", token => api.Search.SeasonAsync(data.SeasonYear, Enum.Parse<Season>(data.Season, true), limit: 5, cancellationToken: token), value =>
        {
            ListResult(value);
            Need(value.Items.Count > 0, "The configured season returned no titles.");
        });
        await Check("new.catalog.anime", token => api.Titles.AnimeAsync(ids.Anime, token), value =>
            Need(value.Id == ids.Anime && !string.IsNullOrWhiteSpace(value.Title), "Invalid anime catalog response."), enabled: ids.Anime > 0, skip: "No animeId is available.");
        await Check("new.catalog.character", token => api.Characters.CatalogAsync(ids.Character, token), value =>
            Need(value.Id == ids.Character && !string.IsNullOrWhiteSpace(value.FirstName), "Invalid character catalog response."), enabled: ids.Character > 0, skip: "No characterId is available.");
        await Check("new.users.stats", token => api.Users.StatsAsync(ids.User, token), value =>
            Need(value.UserId == ids.User && value.Anime is not null && value.Manga is not null, "Invalid user statistics."), enabled: ids.User > 0, skip: "No userId is available.");
        await Check("compat.users.info", token => api.Users.InfoAsync(ids.User, token), value =>
            Need(value.Id == ids.User && !string.IsNullOrWhiteSpace(value.Name), "Invalid user profile."),
            enabled: authenticated && ids.User > 0, skip: "No new API token or userId is available.");
        await Check("guest.compat.last-view", token => guest.Users.LastWatchedAsync(ids.User, limit: 5, cancellationToken: token),
            enabled: ids.User > 0, skip: "No userId is available.", allowEmpty404: true);
        await Check("guest.compat.last-read", token => guest.Users.LastReadAsync(ids.User, limit: 5, cancellationToken: token),
            enabled: ids.User > 0, skip: "No userId is available.", allowEmpty404: true);
        await Check("compat.characters.info", token => api.Characters.InfoAsync(ids.Character, token), value =>
            Need(value.Id == ids.Character && !string.IsNullOrWhiteSpace(value.FirstName), "Invalid character details."),
            enabled: authenticated && ids.Character > 0, skip: "No new API token or characterId is available.");
        await Check("compat.titles.info", token => api.Titles.InfoAsync(ids.Anime, token), value =>
            Need(value.Title is not null && value.Title.Id == ids.Anime, "The title is missing from the info response."),
            enabled: authenticated && ids.Anime > 0, skip: "No new API token or animeId is available.");
        await Check("compat.titles.episodes", token => api.Titles.EpisodesAsync(ids.Anime, token), value =>
            Need(value.Episodes is not null && value.ConnectedEpisodes is not null, "Episode lists are missing."),
            enabled: authenticated && ids.Anime > 0, skip: "No new API token or animeId is available.");
        await Check("compat.titles.characters", token => api.Titles.CharactersAsync(ids.Anime, token), value =>
            Need(value.Relations is not null, "The character relations list is missing."),
            enabled: authenticated && ids.Anime > 0, skip: "No new API token or animeId is available.");
        await Check("compat.episodes.new", token => api.Episodes.NewAsync(token), enabled: authenticated, skip: "New API token tests are disabled.");
        await Check("compat.current-season", token => api.Episodes.CurrentSeasonAsync(token), SeasonalLists,
            enabled: authenticated, skip: "New API token tests are disabled.");
        // The season before the configured one, so the response must differ from the current list.
        var configured = Enum.Parse<Season>(data.Season, true);
        var (previousYear, previous) = configured == Season.Winter ? (data.SeasonYear - 1, Season.Fall) : (data.SeasonYear, configured - 1);
        await Check("compat.current-season.by-season", token => api.Episodes.CurrentSeasonAsync(previousYear, previous, token), value =>
        {
            SeasonalLists(value);
            Need(value.Season.Year == previousYear && value.Season.Value == previous,
                "The response season does not match the requested year and season.");
        }, enabled: authenticated, skip: "New API token tests are disabled.");
        await Check("bulk.compat.characters.all", token => api.Characters.FlatListAsync(token), NonEmpty,
            enabled: authenticated && _settings.IncludeBulkCharacterLists, skip: "Bulk lists or token tests are disabled.");
        await Check("bulk.compat.characters.anime", token => api.Characters.AnimeFlatListAsync(token), NonEmpty,
            enabled: authenticated && _settings.IncludeBulkCharacterLists, skip: "Bulk lists or token tests are disabled.");
        await PocketWaifuChecksAsync(api, ids.User);
        return ids;
    }

    private async Task PocketWaifuChecksAsync(Shinden api, ulong userId)
    {
        var enabled = _settings.IncludePocketWaifu;
        var owner = _settings.Data.PocketWaifuUserId != 0 ? _settings.Data.PocketWaifuUserId : userId;
        var options = new PwCardOptions { Limit = 48 };
        await Check("pw.genres", token => api.PocketWaifu.GenresAsync(token), value =>
            Need(value.Items is not null && value.Dere is not null, "The Pocket Waifu dictionary is missing."), enabled: enabled, skip: "Pocket Waifu checks are disabled.");
        var page = await Check("pw.cards", token => api.PocketWaifu.CardsAsync(owner, options, token), CardsPage,
            enabled: enabled && owner > 0, skip: "Pocket Waifu checks are disabled or no player ID is available.");
        await Check("pw.cards.next-page", token => api.PocketWaifu.CardsAsync(owner,
            new PwCardOptions { Limit = 48, Cursor = page!.NextCursor }, token), CardsPage,
            enabled: enabled && !string.IsNullOrEmpty(page?.NextCursor), skip: "No next card page is available.");
        await Check("pw.cards.ids", token => api.PocketWaifu.CardIdsAsync(owner, options, token), value =>
            Need(value.Ids is not null && value.Total >= value.Ids.Count, "Invalid card ID list."),
            enabled: enabled && owner > 0, skip: "Pocket Waifu checks are disabled or no player ID is available.");
        await Check("pw.tags", token => api.PocketWaifu.TagsAsync(owner, token), value =>
            Need(!string.IsNullOrWhiteSpace(value.Status), "The Pocket Waifu tag status is missing."),
            enabled: enabled && owner > 0, skip: "Pocket Waifu checks are disabled or no player ID is available.");
        var card = _settings.Data.CardId != 0 ? _settings.Data.CardId : page?.Items?.FirstOrDefault()?.Id ?? 0;
        await Check("pw.card", token => api.PocketWaifu.CardAsync(card, token), value =>
            Need(value.Card is not null && value.Card.Id == card, "Invalid card details."),
            enabled: enabled && card > 0, skip: "No card is available; set data.cardId or choose a player with cards.");
    }

    private async Task LegacyChecksAsync(Shinden api, Resources ids)
    {
        var enabled = _settings.LegacyApi.Enabled;
        if (!DeferFavouriteReads)
            await Check("php.users.favourite-characters", token => api.Users.FavouriteCharactersAsync(ids.User, token),
                enabled: enabled && ids.User > 0, skip: "PHP checks are disabled or no userId is available.",
                allowEmpty404: true, empty404Details: "HTTP 404: the favourite character list is empty; its contents were not verified.");
        await Check("php.titles.episode-range", token => api.Titles.EpisodeRangeAsync(ids.Anime, token), value =>
            Need(value.Min is not null || value.Max is not null || value.Status is not null, "The episode range response is empty."),
            enabled: enabled && ids.Anime > 0, skip: "PHP checks are disabled or no animeId is available.");
        await Check("php.titles.recommendations", token => api.Titles.RecommendationsAsync(ids.Anime, token), value =>
            Need(value.Recommendations is not null && value.Recommendations.All(row => row.Id > 0 && row.Content is not null
                && row.Author is not null && row.RecommendedTitle?.Id > 0), "Recommendation content, IDs or nested models are missing."),
            enabled: enabled && ids.Anime > 0, skip: "PHP checks are disabled or no animeId is available.");
        await Check("php.titles.reviews", token => api.Titles.ReviewsAsync(ids.Anime, token), value =>
            Need(value.Reviews is not null && value.Reviews.All(row => row.Id > 0 && row.TitleId == ids.Anime
                && row.Content is not null && row.Author is not null), "Review content, IDs or author data are missing."),
            enabled: enabled && ids.Anime > 0, skip: "PHP checks are disabled or no animeId is available.");
        await Check("php.titles.relations", token => api.Titles.RelationsAsync(ids.Anime, token), value =>
            Need(value.RelatedTitles is not null, "The title relations list is missing."), enabled: enabled && ids.Anime > 0, skip: "PHP checks are disabled or no animeId is available.");
        await Check("php.staff.info", token => api.People.StaffInfoAsync(ids.Staff, token), value =>
            Need(value.Id == ids.Staff || value.StaffId == ids.Staff, "Invalid staff details."), enabled: enabled && ids.Staff > 0, skip: "PHP checks are disabled or no staffId is available.");
        var bulk = enabled && _settings.IncludeBulkCharacterLists;
        await Check("bulk.php.characters.connected", token => api.Characters.ConnectedIdsAsync(token), NonEmpty, enabled: bulk, skip: "PHP checks or bulk lists are disabled.");
        await Check("bulk.php.characters.anime-ids", token => api.Characters.AnimeIdsAsync(token), NonEmpty, enabled: bulk, skip: "PHP checks or bulk lists are disabled.");
        await Check("bulk.php.characters.manga-ids", token => api.Characters.MangaIdsAsync(token), NonEmpty, enabled: bulk, skip: "PHP checks or bulk lists are disabled.");
        await Check("bulk.php.characters.manga-flat", token => api.Characters.MangaFlatListAsync(token), NonEmpty, enabled: bulk, skip: "PHP checks or bulk lists are disabled.");
    }

    private async Task WriteChecksAsync(Shinden api, Shinden keyApi, ulong userId)
    {
        var writes = _settings.Writes;
        var favourite = writes.Favourite;
        if (favourite.Enabled && favourite.Type == "character")
            await CharacterFavouritesAsync(api, keyApi, userId);
        else if (favourite.Enabled)
        {
            var config = new FavouriteQueryConfig { UserId = userId, ObjectId = favourite.ObjectId,
                Type = Enum.Parse<FavouriteQueryConfig.FavType>(favourite.Type, true) };
            try
            {
                await Check("write.favourite", token => favourite.InitiallyFavourite!.Value
                    ? api.Users.RemoveFromFavouritesAsync(config, token) : api.Users.AddToFavouritesAsync(config, token), Changed);
            }
            finally
            {
                await Cleanup("cleanup.favourite", token => favourite.InitiallyFavourite!.Value
                    ? api.Users.AddToFavouritesAsync(config, token) : api.Users.RemoveFromFavouritesAsync(config, token), Changed,
                    $"Object {favourite.Type}/{favourite.ObjectId}; initial favourite state: {favourite.InitiallyFavourite}.");
            }
        }
        else Skip("write.favourite", "The scenario is disabled.");
        var status = writes.TitleStatus;
        if (status.Enabled)
        {
            var removed = false;
            try
            {
                var changed = await Check("write.title-status.change", token => api.Users.ChangeTitleStatusAsync(userId, status.TitleId, status.TestStatus, token),
                    value => Need(value.TitleStatus?.WatchStatus == status.TestStatus, "The response does not confirm the requested list status."));
                if (changed is not null)
                    removed = await Check("write.title-status.remove", token => api.Users.RemoveTitleStatusAsync(userId, status.TitleId, token),
                        value => Need(SuccessStatus(value.Status), "List status removal was not confirmed.")) is not null;
                else Skip("write.title-status.remove", "The list status change was not confirmed.");
            }
            finally
            {
                if (status.InitialStatus is null && removed)
                    Skip("cleanup.title-status", "The scenario removed the list entry; the initial state (no entry) is already restored.");
                else
                    await Cleanup("cleanup.title-status", token => status.InitialStatus is null
                        ? api.Users.RemoveTitleStatusAsync(userId, status.TitleId, token)
                        : api.Users.ChangeTitleStatusAsync(userId, status.TitleId, status.InitialStatus, token),
                        value => Need(SuccessStatus(value.Status), "List status restoration was not confirmed."),
                        $"User {userId}, title {status.TitleId}; previous list status: {status.InitialStatus ?? "no entry"}.");
            }
        }
        else Skip("write.title-status", "The scenario is disabled.");
        await RatingAsync(api, writes.AnimeRating, manga: false);
        await RatingAsync(api, writes.MangaRating, manga: true);
        await Check("write.increase-watched", token => api.Users.IncreaseWatchedEpisodeAsync(userId, writes.IncreaseWatched.TitleId, token), value =>
            Need(!string.IsNullOrWhiteSpace(value.EpisodeAdded), "The watched episode count increase was not confirmed."), enabled: writes.IncreaseWatched.Enabled,
            skip: "The permanent watched episode count increase scenario is disabled.");
    }

    private async Task RatingAsync(Shinden api, RatingWrite settings, bool manga)
    {
        var name = manga ? "manga-rating" : "anime-rating";
        if (!settings.Enabled) { Skip("write." + name, "The scenario is disabled."); return; }
        Task<ErrorOr<Status>> Rate(uint value, CancellationToken token) => manga
            ? api.Users.RateMangaAsync(settings.TitleId, settings.Category, value, token)
            : api.Users.RateAnimeAsync(settings.TitleId, settings.Category, value, token);
        try
        {
            await Check("write." + name, token => Rate(settings.TestValue, token), RatingStatus);
        }
        finally
        {
            await Cleanup("cleanup." + name, token => Rate(settings.RestoreValue!.Value, token), RatingStatus,
                $"Title {settings.TitleId}, category {settings.Category}; previous rating: {settings.RestoreValue}.");
        }
    }

    private async Task CharacterFavouritesAsync(Shinden sessionApi, Shinden keyApi, ulong userId)
    {
        var favourite = _settings.Writes.Favourite;
        var initiallyFavourite = favourite.InitiallyFavourite!.Value;
        var config = new FavouriteQueryConfig { UserId = userId, ObjectId = favourite.ObjectId, Type = FavouriteQueryConfig.FavType.Character };
        var initial = await Check("write.favourite.initial-state", token => ReadFavouriteCharacters(sessionApi, userId, true, token),
            values => Need(values.Any(value => value.Id == favourite.ObjectId) == initiallyFavourite,
                "The current favourite state differs from initiallyFavourite. No favourite writes were sent."));
        if (initial is null)
        {
            Skip("write.favourite", "The initial favourite state was not confirmed.");
            SkipFavouriteReads("The initial favourite state was not confirmed.");
            return;
        }

        var attempted = false;
        try
        {
            if (initiallyFavourite)
            {
                var removed = await Check("write.favourite.prepare-remove", token =>
                {
                    attempted = true;
                    return sessionApi.Users.RemoveFromFavouritesAsync(config, token);
                }, Changed);
                if (removed is null)
                {
                    Skip("write.favourite", "Preparing the favourite for an add/read/remove check failed.");
                    SkipFavouriteReads("Preparing the favourite for the read checks failed.");
                    return;
                }
            }

            var added = await Check("write.favourite", token =>
            {
                attempted = true;
                return sessionApi.Users.AddToFavouritesAsync(config, token);
            }, Changed);
            if (added is null)
            {
                SkipFavouriteReads("Adding the test character was not confirmed.");
                return;
            }

            await VerifyFavouriteReads(sessionApi, keyApi, userId, favourite.ObjectId, present: true);
            if (initiallyFavourite)
            {
                await Check("write.favourite.remove", token => sessionApi.Users.RemoveFromFavouritesAsync(config, token), Changed);
                await VerifyFavouriteReads(sessionApi, keyApi, userId, favourite.ObjectId, present: false, suffix: ".after-remove");
            }
        }
        finally
        {
            if (attempted)
            {
                await Cleanup("cleanup.favourite", token => initiallyFavourite
                    ? sessionApi.Users.AddToFavouritesAsync(config, token) : sessionApi.Users.RemoveFromFavouritesAsync(config, token),
                    value => Need(long.TryParse(value.Updated, out var count) && count >= 0, "The favourite restoration response is invalid."),
                    $"Object character/{favourite.ObjectId}; initial favourite state: {initiallyFavourite}.");
                await VerifyFavouriteReads(sessionApi, keyApi, userId, favourite.ObjectId, initiallyFavourite,
                    suffix: initiallyFavourite ? ".restored" : ".after-remove", independent: true);
            }
        }
    }

    private async Task VerifyFavouriteReads(Shinden sessionApi, Shinden keyApi, ulong userId, ulong characterId,
        bool present, string suffix = "", bool independent = false)
    {
        using var deadline = independent ? new CancellationTokenSource(TimeSpan.FromSeconds(_settings.RequestTimeoutSeconds * 2 + 5)) : null;
        foreach (var (name, client) in new[] { ("php.users.favourite-characters", keyApi), ("php.session.read-favourites", sessionApi) })
            await Check(name + suffix, token => ReadFavouriteCharacters(client, userId, !present, token),
                values => Need(values.Any(value => value.Id == characterId) == present,
                    present ? "The test character is missing from favourites after adding it." : "The test character is still present after removal."),
                independentToken: deadline?.Token,
                recovery: independent ? $"Object character/{characterId}; expected favourite state: {present}." : null);
    }

    private static async Task<ErrorOr<List<FavouriteCharacter>>> ReadFavouriteCharacters(Shinden api, ulong userId,
        bool allowEmpty, CancellationToken token)
    {
        var result = await api.Users.FavouriteCharactersAsync(userId, token);
        return allowEmpty && result.IsError() && result.Error.Type == ErrorType.Http && result.Error.Code == 404
            && string.IsNullOrWhiteSpace(result.Error.RawResponse)
            ? ErrorOr<List<FavouriteCharacter>>.Success(new List<FavouriteCharacter>()) : result;
    }

    private void SkipFavouriteReads(string reason)
    {
        Skip("php.users.favourite-characters", reason);
        Skip("php.session.read-favourites", reason);
    }

    private async Task Cleanup<T>(string name, Func<CancellationToken, Task<ErrorOr<T>>> action, Action<T> validate, string recovery) where T : class
    {
        // A cancelled request might have reached the server. Attempt restoration with its own deadline.
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(_settings.RequestTimeoutSeconds + 5));
        await Check(name, action, validate, independentToken: cleanup.Token, recovery: recovery);
    }

    private async Task<T?> Check<T>(string name, Func<CancellationToken, Task<ErrorOr<T>>> action, Action<T>? validate = null,
        bool enabled = true, string? skip = null, bool allowEmpty404 = false,
        CancellationToken? independentToken = null, string? recovery = null, string? empty404Details = null) where T : class
    {
        if (_only is not null && !name.StartsWith(_only, StringComparison.Ordinal)) { Skip(name, $"Filtered out by --only {_only}."); return null; }
        if (!enabled) { Skip(name, skip ?? "The scenario is disabled."); return null; }
        var token = independentToken ?? _cancellation;
        var result = new CheckResult { Name = name, Status = "FAIL" };
        var timer = Stopwatch.StartNew();
        try
        {
            await _spacing(token);
            var response = await action(token);
            if (!response.TryGet(out var value))
            {
                var error = response.Error;
                result.HttpStatus = error.Type == ErrorType.Http ? error.Code : null;
                result.ErrorType = error.Type.ToString();
                result.ApiCode = SafeCode(error.ApiCode);
                result.JsonPath = ParsingPath(error.Message);
                result.Status = allowEmpty404 && error.Type == ErrorType.Http && error.Code == 404
                    && string.IsNullOrWhiteSpace(error.RawResponse) ? "SKIP" : "FAIL";
                result.Details = result.Status == "SKIP" ? empty404Details ?? "HTTP 404: no history; list contents were not verified."
                    : $"The call failed ({error.Type}); the response and credentials are not recorded.";
                if (error.RetryAfterSeconds.HasValue) result.Details += $" Retry-After: {error.RetryAfterSeconds.Value} s.";
                return null;
            }
            validate?.Invoke(value);
            result.Status = "PASS";
            return value;
        }
        catch (CheckFailure error) { result.Details = error.Message; return null; }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested && !independentToken.HasValue)
        {
            result.Details = "Cancelled by the user.";
            throw;
        }
        catch (Exception error)
        {
            result.ErrorType = error.GetType().Name;
            result.Details = "An exception occurred during the check; its message was omitted to avoid recording credentials.";
            return null;
        }
        finally
        {
            result.DurationMilliseconds = timer.ElapsedMilliseconds;
            if (result.Status == "FAIL" && recovery is not null)
                result.Details += " Manual restoration verification required: " + recovery;
            Add(result);
        }
    }

    private void SkipWrites(string reason)
    {
        if (DeferFavouriteReads) SkipFavouriteReads(reason);
        foreach (var name in new[] { "favourite", "title-status", "anime-rating", "manga-rating", "increase-watched" }) Skip("write." + name, reason);
    }

    private static void SeasonalLists(SeasonalAnimeInfo value)
    {
        Need(value.TitleIds is { Count: > 0 } && value.CharacterIds is not null, "Current season IDs are missing.");
        Need(value.Season is { Year: >= 1900 and <= 2100, Value: not null },
            "The season field is missing or invalid.");
        Need(value.UpdatedAt.HasValue, "The updated_at field is missing.");
        Need(value.CharacterTitles is not null && value.CharacterTitles.All(link => link.CharacterId > 0 && link.TitleId > 0),
            "The character_titles field is missing or has invalid IDs.");
    }

    private void Skip(string name, string reason) => Add(new CheckResult { Name = name, Status = "SKIP", Details = reason });
    private void Add(CheckResult result)
    {
        result.ApiCode = Redact(result.ApiCode);
        result.JsonPath = Redact(result.JsonPath);
        result.Details = Redact(result.Details);
        Report.Checks.Add(result);
        var code = result.HttpStatus.HasValue ? $" HTTP {result.HttpStatus}" : "";
        var diagnostic = string.Join(" ", new[] { result.ErrorType, result.ApiCode, result.JsonPath }.Where(value => !string.IsNullOrEmpty(value)));
        _output.WriteLine($"[{result.Status}] {result.Name}{code} ({result.DurationMilliseconds} ms) {diagnostic} {result.Details}");
    }

    private string? Redact(string? value)
    {
        if (value is null) return null;
        foreach (var secret in new[] { _settings.NewApi.Authentication.BearerToken, _settings.LegacyApi.Authentication.ApiKey,
            _settings.LegacyApi.UserAuthentication.Password, _settings.LegacyApi.UserAuthentication.Username })
        {
            if (!string.IsNullOrEmpty(secret)) value = value.Replace(secret, "[redacted]").Replace(Uri.EscapeDataString(secret), "[redacted]");
        }
        return value;
    }
    private static void Need(bool valid, string message) { if (!valid) throw new CheckFailure(message); }
    private static void NonEmpty<T>(List<T> values) => Need(values is { Count: > 0 }, "The list is empty. Check the search query or selected resource.");
    private static void ListResult(SearchResult result) => Need(result.Items is not null && result.Total >= result.Items.Count, "The items/total result is missing or invalid.");
    private static void CardsPage(PwCardsPage result) => Need(new[] { "ok", "none", "pending", "disabled" }.Contains(result.Status)
        && (result.Status != "ok" || result.Items is not null), "Invalid Pocket Waifu card page.");
    private static void Changed(ModificationResult result) => Need(long.TryParse(result.Updated, out var updated) && updated > 0, "The API did not confirm the favourite change.");
    private static bool SuccessStatus(string? status) => status is "ok" or "success" or "updated" or "deleted" or "removed";
    private static void RatingStatus(Status result) => Need(SuccessStatus(result.ResponseStatus), "The API did not confirm the rating update.");

    // Include structural JSON paths only, never the invalid response value.
    internal static string? ParsingPath(string? message)
    {
        var match = Regex.Match(message ?? "", @"Path '([A-Za-z_][A-Za-z0-9_.\[\]-]{0,150})'");
        return match.Success ? match.Groups[1].Value : null;
    }
    private static string? SafeCode(string? code)
        => code is not null && code.Length <= 80 && Regex.IsMatch(code, "^[a-z_]+$") ? code : null;
}
