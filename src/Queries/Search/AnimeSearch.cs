using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using ShindenAPI.Models;

namespace ShindenAPI.Queries.Search;

public sealed class AnimeSearchOptions
{
    internal IReadOnlyList<SearchParameter> Values { get; }

    internal AnimeSearchOptions(IReadOnlyList<SearchParameter> values) => Values = values;
}

internal readonly struct SearchParameter
{
    internal SearchParameter(string name, string value, bool indexed)
    {
        Name = name;
        Value = value;
        Indexed = indexed;
    }

    internal string Name { get; }
    internal string Value { get; }
    internal bool Indexed { get; }
}

public sealed class AnimeSearchOptionsBuilder
{
    private readonly List<SearchParameter> _values = new();

    public AnimeSearchOptionsBuilder With(string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(name) && value is not null)
        {
            _values.RemoveAll(entry => !entry.Indexed && entry.Name == name);
            _values.Add(new SearchParameter(name, value, false));
        }

        return this;
    }

    public AnimeSearchOptionsBuilder WithQuery(string query) => With("search", query);

    public AnimeSearchOptionsBuilder WithPage(uint page)
        => page > 0 ? With("page", page.ToString(CultureInfo.InvariantCulture))
            : throw new ArgumentOutOfRangeException(nameof(page));

    public AnimeSearchOptionsBuilder WithPageSize(uint pageSize)
        => pageSize > 0 ? With("limit", pageSize.ToString(CultureInfo.InvariantCulture))
            : throw new ArgumentOutOfRangeException(nameof(pageSize));

    public AnimeSearchOptionsBuilder WithSort(string field, bool descending = false)
        => With("sort_by", field).With("sort_order", descending ? "desc" : "asc");

    public AnimeSearchOptionsBuilder WithSortBy(string field) => With("sort_by", field);

    public AnimeSearchOptionsBuilder WithSortOrder(string order) => With("sort_order", order);

    public AnimeSearchOptionsBuilder WithDatePrecision(uint precision)
        => With("start_date_precision", precision.ToString(CultureInfo.InvariantCulture));

    public AnimeSearchOptionsBuilder WithTagInclude(string includeType) => With("genres-type", includeType);

    public AnimeSearchOptionsBuilder WithSearchType(string searchType) => With("type", searchType);

    public AnimeSearchOptionsBuilder WithFirstLetter(char letter) => With("letter", letter.ToString());

    public AnimeSearchOptionsBuilder WithYearRange(string from, string to)
        => With("year_from", from).With("year_to", to);

    public AnimeSearchOptionsBuilder WithEpisodeCountRange(uint from, uint to)
        => With("series_number_from", from.ToString(CultureInfo.InvariantCulture))
            .With("series_number_to", to.ToString(CultureInfo.InvariantCulture));

    public AnimeSearchOptionsBuilder WithType(string type) => WithIndexed("series_type", type);

    public AnimeSearchOptionsBuilder WithStatus(string status) => WithIndexed("series_status", status);

    public AnimeSearchOptionsBuilder WithEpisodeLength(string length) => WithIndexed("series_length", length);

    public AnimeSearchOptionsBuilder WithEpisodeCount(string count) => WithIndexed("series_number", count);

    public AnimeSearchOptionsBuilder WithGenre(string genreId) => With("genres", genreId);

    public AnimeSearchOptionsBuilder WithPlayersAvailable(bool value)
        => With("one_online", value ? "1" : "0");

    public AnimeSearchOptionsBuilder WithIndexed(string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(name) && value is not null)
            _values.Add(new SearchParameter(name, value, true));

        return this;
    }

    public AnimeSearchOptions Build() => new(new List<SearchParameter>(_values));
}

public sealed class AnimeSearch : Request<SearchResult>
{
    private readonly AnimeSearchOptions _options;

    public AnimeSearch(AnimeSearchOptions options) => _options = options ?? new AnimeSearchOptionsBuilder().Build();

    public AnimeSearch(Func<AnimeSearchOptionsBuilder, AnimeSearchOptions> configure)
        : this(configure(new AnimeSearchOptionsBuilder())) { }

    internal override HttpRequestMessage Build(Uri uri, string token)
        => BrowseRequest.Build(uri, "anime", _options);
}

public sealed class MangaSearch : Request<SearchResult>
{
    private readonly AnimeSearchOptions _options;
    public MangaSearch(AnimeSearchOptions options) => _options = options ?? new AnimeSearchOptionsBuilder().Build();
    internal override HttpRequestMessage Build(Uri uri, string token) => BrowseRequest.Build(uri, "manga", _options);
}

internal static class BrowseRequest
{
    internal static HttpRequestMessage Build(Uri uri, string obj, AnimeSearchOptions options)
    {
        var parameters = options.Values.Select(entry => new KeyValuePair<string, string>(
            entry.Indexed ? entry.Name + "[]" : entry.Name, entry.Value)).ToArray();
        return new HttpRequestMessage(HttpMethod.Get, RequestUri.Create(uri, "v1/search/browse/" + obj, parameters));
    }
}
