using System.Net.Http;
using System;
using System.Linq;
using ShindenAPI.Models;
using Newtonsoft.Json.Linq;

namespace ShindenAPI.Queries.Title;

public sealed class TitleInfo : GetRequest<AnimeMangaInfo>
{
    public TitleInfo(ulong titleId, string language = "pl")
        : base($"compat/php-api/title/{titleId}/info", RequestUri.Parameter("lang", language)) { }
}

public sealed class Episodes : GetRequest<TitleEpisodes>
{
    public Episodes(ulong titleId, string language = "pl")
        : base($"compat/php-api/title/{titleId}/episodes", RequestUri.Parameter("lang", language)) { }

    // Non-anime titles return 200 without a body in the compatibility API.
    internal override TitleEpisodes Parse(string response)
        => string.IsNullOrWhiteSpace(response) ? new TitleEpisodes() : base.Parse(response);
}

public sealed class Characters : GetRequest<TitleCharacters>
{
    public Characters(ulong titleId, string language = "pl")
        : base($"compat/php-api/title/{titleId}/characters", RequestUri.Parameter("lang", language)) { }
}


public sealed class EpisodeRange : LegacyRequest<EpisodesRange>
{
    private readonly ulong _titleId;

    public EpisodeRange(ulong titleId) => _titleId = titleId;

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, $"api/episode/{_titleId}/range",
            RequestUri.Parameter("api_key", token))
    };
}

public sealed class Recommendations : LegacyRequest<TitleRecommendations>
{
    private readonly ulong _titleId;

    public Recommendations(ulong titleId) => _titleId = titleId;

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, $"api/title/{_titleId}/recommendations",
            RequestUri.Parameter("api_key", token))
    };
}

public sealed class Reviews : LegacyRequest<TitleReviews>
{
    private readonly ulong _titleId;

    public Reviews(ulong titleId) => _titleId = titleId;

    internal override TitleReviews Parse(string response)
    {
        var root = JObject.Parse(response);
        if (root["reviews"] is JArray reviews)
        {
            foreach (var row in reviews.OfType<JObject>())
                if (row.Property("title_id") is null) row["title_id"] = _titleId;
        }
        return root.ToObject<TitleReviews>();
    }

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, $"api/title/{_titleId}/reviews",
            RequestUri.Parameter("api_key", token))
    };
}

public sealed class Relations : LegacyRequest<TitleRelations>
{
    private readonly ulong _titleId;

    public Relations(ulong titleId) => _titleId = titleId;

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, $"api/title/{_titleId}/related",
            RequestUri.Parameter("api_key", token))
    };
}
