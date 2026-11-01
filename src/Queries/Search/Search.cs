using System;
using System.Collections.Generic;
using System.Net.Http;

namespace ShindenAPI.Queries.Search;

public class Search<TReturn> : Request<List<TReturn>>
{
    private readonly string _query;
    private readonly string _what;
    private readonly KeyValuePair<string, string>[] _parameters;

    protected Search(string query) : this(null, query) { }

    protected Search(string what, string query) : this(what, query, Array.Empty<KeyValuePair<string, string>>()) { }

    protected Search(string what, string query, params KeyValuePair<string, string>[] parameters)
    {
        _what = what;
        _query = query;
        _parameters = parameters ?? Array.Empty<KeyValuePair<string, string>>();
    }

    internal override HttpRequestMessage Build(Uri uri, string token)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            RequestUri.Parameter("query", _query)
        };
        parameters.AddRange(_parameters);

        return new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = RequestUri.Create(uri, "v1/search/" + _what, parameters.ToArray())
        };
    }
}
