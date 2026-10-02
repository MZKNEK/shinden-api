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
            _values.Add(new SearchParameter(name, value, false));

        return this;
    }

    public AnimeSearchOptionsBuilder WithQuery(string query) => With("search", query);

    public AnimeSearchOptionsBuilder WithOffset(uint offset)
        => With("offset", offset.ToString(CultureInfo.InvariantCulture));

    public AnimeSearchOptionsBuilder WithPageSize(uint pageSize)
        => With("per_page", pageSize.ToString(CultureInfo.InvariantCulture));

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
    {
        var form = new List<KeyValuePair<string, string>>
        {
            RequestUri.Parameter("for_seasons", 1)
        };

        var indexes = new Dictionary<string, uint>();
        var hasOffset = false;
        foreach (var entry in _options.Values)
        {
            var name = entry.Name;
            hasOffset |= !entry.Indexed && entry.Name == "offset";
            if (entry.Indexed)
            {
                indexes.TryGetValue(entry.Name, out var index);
                name = $"{entry.Name}[{index}]";
                indexes[entry.Name] = index + 1;
            }

            form.Add(new KeyValuePair<string, string>(name, entry.Value));
        }

        if (!hasOffset)
            form.Insert(0, RequestUri.Parameter("offset", 0));

        return new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = RequestUri.Create(uri, "api/anime/search",
                RequestUri.Parameter("api_key", token),
                RequestUri.Parameter("lang", "pl"),
                RequestUri.Parameter("decode", 1),
                RequestUri.Parameter("for_season", 1)),
            Content = RequestUri.Form(form.ToArray())
        };
    }

    internal override bool RequireHttps() => true;
}
