using System;
using System.Collections.Generic;
using System.Net.Http;

namespace ShindenAPI.Queries.Search;

public class Search<TReturn> : Request<List<TReturn>>
{
    private readonly string _query;
    private readonly string _what;

    protected Search(string query) => _query = query;

    protected Search(string what, string query) : this(query) => _what = what;

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(uri, "api/" + _what + "/search?query=" + _query)
        };
}
