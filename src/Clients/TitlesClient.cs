using ShindenAPI.Queries.Title;
using ShindenAPI.Queries.Episode;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShindenAPI.Models;


namespace ShindenAPI;

public sealed class TitlesClient
{
    private readonly RequestManager _manager;
    private readonly RequestManager _legacyManager;
    internal TitlesClient(RequestManager manager, RequestManager legacyManager)
    {
        _manager = manager;
        _legacyManager = legacyManager;
    }

    public Task<ErrorOr<AnimeMangaInfo>> InfoAsync(ulong titleId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Queries.Title.TitleInfo(titleId), cancellationToken: cancellationToken);

    public Task<ErrorOr<TitleEpisodes>> EpisodesAsync(ulong titleId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Queries.Title.Episodes(titleId), cancellationToken: cancellationToken);

    public Task<ErrorOr<TitleCharacters>> CharactersAsync(ulong titleId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Queries.Title.Characters(titleId), cancellationToken: cancellationToken);

    public Task<ErrorOr<CatalogAnime>> AnimeAsync(ulong titleId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Queries.V1.Anime(titleId), cancellationToken: cancellationToken);


    public Task<ErrorOr<EpisodesRange>> EpisodeRangeAsync(
        ulong titleId, CancellationToken cancellationToken = default)
        => _legacyManager.MakeQueryAsync(new EpisodeRange(titleId), cancellationToken: cancellationToken);

    public Task<ErrorOr<TitleRecommendations>> RecommendationsAsync(
        ulong titleId, CancellationToken cancellationToken = default)
        => _legacyManager.MakeQueryAsync(new Recommendations(titleId), cancellationToken: cancellationToken);

    public Task<ErrorOr<TitleReviews>> ReviewsAsync(
        ulong titleId, CancellationToken cancellationToken = default)
        => _legacyManager.MakeQueryAsync(new Reviews(titleId), cancellationToken: cancellationToken);

    public Task<ErrorOr<TitleRelations>> RelationsAsync(
        ulong titleId, CancellationToken cancellationToken = default)
        => _legacyManager.MakeQueryAsync(new Relations(titleId), cancellationToken: cancellationToken);
}
