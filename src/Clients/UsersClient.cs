using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShindenAPI.Models;
using UserQueries = ShindenAPI.Queries.User;
using FavouriteBuilder = ShindenAPI.Queries.Builder;

namespace ShindenAPI;

/// <summary>Provides user, rating, favourite, and authentication operations.</summary>
public sealed class UsersClient
{
    private readonly RequestManager _manager;

    internal UsersClient(RequestManager manager) => _manager = manager;

    public Task<ErrorOr<UserInfo>> InfoAsync(
        ulong userId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new UserQueries.UserInfo(userId), cancellationToken: cancellationToken);

    public Task<ErrorOr<List<LastWatched>>> LastWatchedAsync(
        ulong userId, uint limit = 5, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new UserQueries.LastWatched(userId, limit), cancellationToken: cancellationToken);

    public Task<ErrorOr<List<LastRead>>> LastReadAsync(
        ulong userId, uint limit = 5, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new UserQueries.LastRead(userId, limit), cancellationToken: cancellationToken);

    public Task<ErrorOr<List<FavouriteCharacter>>> FavouriteCharactersAsync(
        ulong userId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new UserQueries.FavouriteCharacters(userId), cancellationToken: cancellationToken);

    public Task<ErrorOr<TitleStatusAfterChange>> ChangeTitleStatusAsync(
        ulong userId, ulong titleId, string status, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(
            new UserQueries.ChangeTitleStatus(userId, titleId, status),
            cancellationToken: cancellationToken);

    public Task<ErrorOr<TitleStatusAfterChange>> ChangeTitleStatusAsync(
        ulong userId, ulong titleId, TitleListStatus status, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(
            new UserQueries.ChangeTitleStatus(userId, titleId, status),
            cancellationToken: cancellationToken);

    public Task<ErrorOr<TitleStatusAfterChange>> RemoveTitleStatusAsync(
        ulong userId, ulong titleId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(
            new UserQueries.RemoveTitleStatus(userId, titleId),
            cancellationToken: cancellationToken);

    public Task<ErrorOr<IncreaseWatchedResult>> IncreaseWatchedEpisodeAsync(
        ulong userId, ulong titleId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(
            new UserQueries.IncreaseWatchedEpisode(userId, titleId),
            cancellationToken: cancellationToken);

    public Task<ErrorOr<Status>> RateAnimeAsync(
        ulong titleId, string type, uint value, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(
            new UserQueries.RateAnime(titleId, type, value),
            cancellationToken: cancellationToken);

    public Task<ErrorOr<Status>> RateAnimeAsync(
        ulong titleId, AnimeRatingCategory type, uint value, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(
            new UserQueries.RateAnime(titleId, type, value),
            cancellationToken: cancellationToken);

    public Task<ErrorOr<Status>> RateMangaAsync(
        ulong titleId, string type, uint value, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(
            new UserQueries.RateManga(titleId, type, value),
            cancellationToken: cancellationToken);

    public Task<ErrorOr<Status>> RateMangaAsync(
        ulong titleId, MangaRatingCategory type, uint value, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(
            new UserQueries.RateManga(titleId, type, value),
            cancellationToken: cancellationToken);

    public Task<ErrorOr<ModificationResult>> AddToFavouritesAsync(
        FavouriteBuilder.FavouriteQueryConfig config,
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(
            new UserQueries.FavouriteAdd(config),
            cancellationToken: cancellationToken);

    public Task<ErrorOr<ModificationResult>> AddToFavouritesAsync(
        Func<FavouriteBuilder.FavouriteQueryConfigBuilder, FavouriteBuilder.FavouriteQueryConfig> configure,
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(
            new UserQueries.FavouriteAdd(configure(new FavouriteBuilder.FavouriteQueryConfigBuilder())),
            cancellationToken: cancellationToken);

    public Task<ErrorOr<ModificationResult>> RemoveFromFavouritesAsync(
        FavouriteBuilder.FavouriteQueryConfig config,
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(
            new UserQueries.FavouriteRemove(config),
            cancellationToken: cancellationToken);

    public Task<ErrorOr<ModificationResult>> RemoveFromFavouritesAsync(
        Func<FavouriteBuilder.FavouriteQueryConfigBuilder, FavouriteBuilder.FavouriteQueryConfig> configure,
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(
            new UserQueries.FavouriteRemove(configure(new FavouriteBuilder.FavouriteQueryConfigBuilder())),
            cancellationToken: cancellationToken);

    public Task<UserSearchResult> LoginAsync(CancellationToken cancellationToken = default)
        => _manager.ForceLoginAsync(cancellationToken);

    public Task<ErrorOr<UserSearchResult>> LoginResultAsync(
        CancellationToken cancellationToken = default)
        => _manager.LoginAsUserAsync(cancellationToken);

    public bool IsValidSession() => _manager.IsValidUserSession();
}
