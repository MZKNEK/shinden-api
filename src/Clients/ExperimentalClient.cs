using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShindenAPI.Models;
using ShindenAPI.Queries.Character;
using ShindenAPI.Queries.Episode;

namespace ShindenAPI;

/// <summary>Provides access to experimental endpoints and their raw DTO contracts.</summary>
public sealed class ExperimentalClient
{
    private readonly RequestManager _manager;

    internal ExperimentalClient(RequestManager manager) => _manager = manager;

    public Task<ErrorOr<List<CharacterFlat>>> AllCharactersFlatAsync(
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new FlatList(), cancellationToken: cancellationToken);

    public Task<ErrorOr<List<CharacterFlat>>> AnimeCharactersFlatAsync(
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new AnimeFlatList(), cancellationToken: cancellationToken);

    public Task<ErrorOr<List<CharacterFlat>>> MangaCharactersFlatAsync(
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new MangaFlatList(), cancellationToken: cancellationToken);

    public async Task<ErrorOr<List<ulong>>> AnimeFromSeasonAsync(
        bool characters = false, CancellationToken cancellationToken = default)
    {
        var result = await _manager.MakeQueryAsync(
            new CurrentSeason(), cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.IsOk())
            return ErrorOr<List<ulong>>.FromError(result.Error);

        var season = result.Get();
        return ErrorOr<List<ulong>>.From(characters ? season.CharacterIds : season.TitleIds);
    }

    public Task<ErrorOr<List<ulong>>> AnimeCharacterIdsAsync(
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new AnimeIds(), cancellationToken: cancellationToken);

    public Task<ErrorOr<List<ulong>>> MangaCharacterIdsAsync(
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new MangaIds(), cancellationToken: cancellationToken);

    public Task<ErrorOr<List<ulong>>> CharacterIdsAsync(
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new ConnectedIds(), cancellationToken: cancellationToken);
}
