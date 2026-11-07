using System;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using ShindenAPI.Models;
using ShindenAPI.Queries.V1;

namespace ShindenAPI.Queries.Episode;

public sealed class NewEpisodes : Request<List<NewEpisode>>
{
    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, "compat/php-api/episode/new")
    };

    internal override List<NewEpisode> Parse(string response)
    {
        var root = JToken.Parse(response);
        var source = root is JObject obj ? obj["lastonline"] ?? root : root;
        var result = new List<NewEpisode>();

        if (source.Type == JTokenType.Array)
        {
            foreach (var item in source)
                result.Add(item.ToObject<NewEpisode>());
        }
        else if (source.Type == JTokenType.Object)
        {
            foreach (var item in ((JObject)source).Properties())
                result.Add(item.Value.ToObject<NewEpisode>());
        }

        return result;
    }
}

public sealed class CurrentSeason : GetRequest<SeasonalAnimeInfo>
{
    /// <summary>Lists for the season the API currently considers active.</summary>
    public CurrentSeason() : base(PathValue) { }

    /// <summary>Lists for the given season (sent as <c>?year=&amp;season=</c>).</summary>
    public CurrentSeason(int year, Season season)
        : base(PathValue, RequestUri.Parameter("year", ValidYear(year)), RequestUri.Parameter("season", SeasonName(season))) { }

    private const string PathValue = "compat/php-api/anime/current-season";

    private static int ValidYear(int year) => year is >= 1900 and <= 2100
        ? year : throw new ArgumentOutOfRangeException(nameof(year));

    private static string SeasonName(Season season) => season.ToApiName();
}
