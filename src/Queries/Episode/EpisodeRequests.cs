using System;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using ShindenAPI.Models;

namespace ShindenAPI.Queries.Episode;

public sealed class NewEpisodes : Request<List<NewEpisode>>
{
    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, "api/episode/new",
            RequestUri.Parameter("api_key", token))
    };

    internal override List<NewEpisode> Parse(string response)
    {
        var root = JToken.Parse(response);
        var source = root["lastonline"] ?? root;
        var result = new List<NewEpisode>();

        if (source.Type == JTokenType.Array)
        {
            foreach (var item in source)
                result.Add(item.ToObject<NewEpisode>());
        }
        else if (source.Type == JTokenType.Object)
        {
            foreach (var item in source.Children())
                result.Add(item.ToObject<NewEpisode>());
        }

        return result;
    }
}

public sealed class CurrentSeason : Request<SeasonalAnimeInfo>
{
    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, "api/anime/current-season",
            RequestUri.Parameter("api_key", token))
    };
}
