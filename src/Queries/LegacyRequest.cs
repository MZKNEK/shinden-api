using System;
using System.Net.Http;

namespace ShindenAPI.Queries;

/// <summary>A request using the legacy API contract, key and user session.</summary>
public abstract class LegacyRequest<T> : Request<T>
{
    internal sealed override bool IsLegacy => true;

    // Both origin and the historical /api base URI are accepted for PHP requests.
    internal static Uri NormalizeBaseUri(Uri uri)
    {
        if (uri is null) throw new ArgumentNullException(nameof(uri));
        if (!uri.IsAbsoluteUri) throw new ArgumentException("The API URI must be absolute.", nameof(uri));
        var path = uri.AbsolutePath.TrimEnd('/');
        return path.EndsWith("/api", StringComparison.OrdinalIgnoreCase)
            ? new UriBuilder(uri) { Path = path.Substring(0, path.Length - 4) + "/", Query = string.Empty, Fragment = string.Empty }.Uri
            : uri;
    }
}
