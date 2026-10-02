using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace ShindenAPI.Queries;

internal static class RequestUri
{
    internal static Uri Create(Uri baseUri, string relativePath, params KeyValuePair<string, string>[] parameters)
    {
        if (baseUri is null)
            throw new ArgumentNullException(nameof(baseUri));
        if (!baseUri.IsAbsoluteUri)
            throw new ArgumentException("The API URI must be absolute.", nameof(baseUri));
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("A relative API path is required.", nameof(relativePath));

        var baseBuilder = new UriBuilder(baseUri)
        {
            Path = baseUri.AbsolutePath.TrimEnd('/')
        };
        if (string.IsNullOrEmpty(baseBuilder.Path))
            baseBuilder.Path = "/";

        var uri = new Uri(baseBuilder.Uri, relativePath.TrimStart('/'));
        if (parameters is null || parameters.Length == 0)
            return uri;

        var query = new StringBuilder();
        foreach (var parameter in parameters)
        {
            if (string.IsNullOrEmpty(parameter.Key) || parameter.Value is null)
                continue;

            if (query.Length > 0)
                query.Append('&');

            query.Append(Uri.EscapeDataString(parameter.Key));
            query.Append('=');
            query.Append(Uri.EscapeDataString(parameter.Value));
        }

        if (query.Length == 0)
            return uri;

        var builder = new UriBuilder(uri) { Query = query.ToString() };
        return builder.Uri;
    }

    internal static HttpContent Form(params KeyValuePair<string, string>[] values)
        => new FormUrlEncodedContent(values);

    internal static KeyValuePair<string, string> Parameter(string name, object value)
        => new(name, value?.ToString());
}
