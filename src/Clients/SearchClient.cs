using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShindenAPI.Models;
using ShindenAPI.Queries.Search;

namespace ShindenAPI;

/// <summary>Provides search operations.</summary>
public sealed class SearchClient
{
    private readonly RequestManager _manager;

    internal SearchClient(RequestManager manager) => _manager = manager;

    public Task<ErrorOr<List<UserSearchResult>>> UsersAsync(string query, uint limit = 10, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new User(query, limit), cancellationToken);

    public Task<ErrorOr<List<CharacterSearchResult>>> CharactersAsync(string query, uint limit = 10, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Character(query, limit), cancellationToken);

    public Task<ErrorOr<List<StaffSearchResult>>> StaffAsync(string query, uint limit = 10, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Staff(query, limit), cancellationToken);

    public Task<ErrorOr<List<TitleSearchResult>>> TitlesAsync(string query, TitleQuick.SearchType type = TitleQuick.SearchType.Both,
        uint limit = 10, ulong? ignoreId = null, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new TitleQuick(query, type, limit, ignoreId), cancellationToken);

    public Task<ErrorOr<List<TitleById>>> TitlesByIdsAsync(IEnumerable<ulong> ids, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Queries.V1.TitlesByIds(ids), cancellationToken);

    public Task<ErrorOr<List<SearchTag>>> TagsAsync(string query, string types = null, uint limit = 10, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Queries.V1.Tags(query, types, limit), cancellationToken);

    public Task<ErrorOr<SearchResult>> MangaAsync(AnimeSearchOptions options, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new MangaSearch(options), cancellationToken);

    public Task<ErrorOr<SearchResult>> SeasonAsync(int year, Queries.V1.Season season, string order = "popular",
        string type = null, uint? limit = null, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Queries.V1.SeasonTitles(year, season, order, type, limit), cancellationToken);

    public Task<ErrorOr<SearchResult>> AnimeAsync(
        AnimeSearchOptions options, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new AnimeSearch(options), cancellationToken: cancellationToken);

    public Task<ErrorOr<SearchResult>> AnimeAsync(
        Func<AnimeSearchOptionsBuilder, AnimeSearchOptions> configure,
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new AnimeSearch(configure), cancellationToken: cancellationToken);
}
