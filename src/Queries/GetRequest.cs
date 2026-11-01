using System;
using System.Collections.Generic;
using System.Net.Http;

namespace ShindenAPI.Queries;

/// <summary>Base class for GET requests against either documented API family.</summary>
public abstract class GetRequest<T> : Request<T>
{
    private readonly string _path;
    private readonly KeyValuePair<string, string>[] _parameters;

    protected GetRequest(string path, params KeyValuePair<string, string>[] parameters)
    {
        _path = path;
        _parameters = parameters;
    }

    internal override HttpRequestMessage Build(Uri uri, string token)
        => new(HttpMethod.Get, RequestUri.Create(uri, _path, _parameters));
}
