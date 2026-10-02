using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShindenAPI.Models;
using ShindenAPI.Queries.Episode;

namespace ShindenAPI;

/// <summary>Provides episode feed operations.</summary>
public sealed class EpisodesClient
{
    private readonly RequestManager _manager;

    internal EpisodesClient(RequestManager manager) => _manager = manager;

    public Task<ErrorOr<List<NewEpisode>>> NewAsync(
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new NewEpisodes(), cancellationToken: cancellationToken);

    public Task<ErrorOr<SeasonalAnimeInfo>> CurrentSeasonAsync(
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new CurrentSeason(), cancellationToken: cancellationToken);
}
