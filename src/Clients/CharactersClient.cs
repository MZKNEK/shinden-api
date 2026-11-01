using ShindenAPI.Queries.Character;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShindenAPI.Models;


namespace ShindenAPI;

public sealed class CharactersClient
{
    private readonly RequestManager _manager;
    private readonly RequestManager _legacyManager;
    internal CharactersClient(RequestManager manager, RequestManager legacyManager)
    {
        _manager = manager;
        _legacyManager = legacyManager;
    }

    public Task<ErrorOr<List<CharacterFlat>>> FlatListAsync(CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Queries.Character.FlatList(), cancellationToken: cancellationToken);

    public Task<ErrorOr<List<CharacterFlat>>> AnimeFlatListAsync(CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Queries.Character.AnimeFlatList(), cancellationToken: cancellationToken);

    public Task<ErrorOr<CharacterInfo>> InfoAsync(ulong characterId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Queries.People.CharacterInfo(characterId), cancellationToken: cancellationToken);

    public Task<ErrorOr<CatalogCharacter>> CatalogAsync(ulong characterId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Queries.V1.Character(characterId), cancellationToken: cancellationToken);


    public Task<ErrorOr<List<ulong>>> ConnectedIdsAsync(
        CancellationToken cancellationToken = default)
        => _legacyManager.MakeQueryAsync(new ConnectedIds(), cancellationToken: cancellationToken);

    public Task<ErrorOr<List<ulong>>> AnimeIdsAsync(
        CancellationToken cancellationToken = default)
        => _legacyManager.MakeQueryAsync(new AnimeIds(), cancellationToken: cancellationToken);

    public Task<ErrorOr<List<ulong>>> MangaIdsAsync(
        CancellationToken cancellationToken = default)
        => _legacyManager.MakeQueryAsync(new MangaIds(), cancellationToken: cancellationToken);

    public Task<ErrorOr<List<CharacterFlat>>> MangaFlatListAsync(
        CancellationToken cancellationToken = default)
        => _legacyManager.MakeQueryAsync(new MangaFlatList(), cancellationToken: cancellationToken);
}
