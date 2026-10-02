using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShindenAPI.Models;
using ShindenAPI.Queries.Character;

namespace ShindenAPI;

/// <summary>Provides character relationship and flat-list operations.</summary>
public sealed class CharactersClient
{
    private readonly RequestManager _manager;

    internal CharactersClient(RequestManager manager) => _manager = manager;

    public Task<ErrorOr<List<ulong>>> ConnectedIdsAsync(
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new ConnectedIds(), cancellationToken: cancellationToken);

    public Task<ErrorOr<List<ulong>>> AnimeIdsAsync(
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new AnimeIds(), cancellationToken: cancellationToken);

    public Task<ErrorOr<List<ulong>>> MangaIdsAsync(
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new MangaIds(), cancellationToken: cancellationToken);

    public Task<ErrorOr<List<CharacterFlat>>> FlatListAsync(
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new FlatList(), cancellationToken: cancellationToken);

    public Task<ErrorOr<List<CharacterFlat>>> AnimeFlatListAsync(
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new AnimeFlatList(), cancellationToken: cancellationToken);

    public Task<ErrorOr<List<CharacterFlat>>> MangaFlatListAsync(
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new MangaFlatList(), cancellationToken: cancellationToken);
}
