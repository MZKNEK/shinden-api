using System.Threading;
using System.Threading.Tasks;
using ShindenAPI.Models;
using ShindenAPI.Queries.Episode;
using ShindenAPI.Queries.Title;

namespace ShindenAPI;

/// <summary>Provides title and episode operations.</summary>
public sealed class TitlesClient
{
    private readonly RequestManager _manager;

    internal TitlesClient(RequestManager manager) => _manager = manager;

    public Task<ErrorOr<AnimeMangaInfo>> InfoAsync(
        ulong titleId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new TitleInfo(titleId), cancellationToken: cancellationToken);

    public Task<ErrorOr<TitleEpisodes>> EpisodesAsync(
        ulong titleId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Episodes(titleId), cancellationToken: cancellationToken);

    public Task<ErrorOr<EpisodesRange>> EpisodeRangeAsync(
        ulong titleId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new EpisodeRange(titleId), cancellationToken: cancellationToken);

    public Task<ErrorOr<TitleRecommendations>> RecommendationsAsync(
        ulong titleId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Recommendations(titleId), cancellationToken: cancellationToken);

    public Task<ErrorOr<TitleReviews>> ReviewsAsync(
        ulong titleId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Reviews(titleId), cancellationToken: cancellationToken);

    public Task<ErrorOr<TitleRelations>> RelationsAsync(
        ulong titleId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Relations(titleId), cancellationToken: cancellationToken);

    public Task<ErrorOr<TitleCharacters>> CharactersAsync(
        ulong titleId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Characters(titleId), cancellationToken: cancellationToken);
}
