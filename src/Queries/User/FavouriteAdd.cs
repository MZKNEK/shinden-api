using System;
using System.Net.Http;
using Shinden.Queries.Builder;
using ShindenAPI.Models;

namespace ShindenAPI.Queries.User;

public class FavouriteAdd : Request<ModificationResult>
{
    private readonly FavouriteQueryConfig _config;

    public FavouriteAdd(FavouriteQueryConfig config) => _config = config;

    public FavouriteAdd(Func<FavouriteQueryConfigBuilder, FavouriteQueryConfig> config)
        : this(config(new FavouriteQueryConfigBuilder())) { }

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        RequestUri = new Uri(uri, "api/userlist/" + _config.UserId + "/fav?api_key=" + token),
        Method = HttpMethod.Post,
        Content = _config.Build()
    };

    internal override bool RequireHttps() => true;
}
