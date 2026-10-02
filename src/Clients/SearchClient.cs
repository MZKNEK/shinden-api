using System;
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

    public Task<ErrorOr<SearchResult>> AnimeAsync(
        AnimeSearchOptions options, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new AnimeSearch(options), cancellationToken: cancellationToken);

    public Task<ErrorOr<SearchResult>> AnimeAsync(
        Func<AnimeSearchOptionsBuilder, AnimeSearchOptions> configure,
        CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new AnimeSearch(configure), cancellationToken: cancellationToken);
}
