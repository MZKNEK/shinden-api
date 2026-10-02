using System;
using System.Net.Http;
using ShindenAPI.Models;

namespace ShindenAPI.Queries.Title;

public sealed class TitleInfo : Request<AnimeMangaInfo>
{
    private readonly ulong _titleId;

    public TitleInfo(ulong titleId) => _titleId = titleId;

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, $"api/title/{_titleId}/info",
            RequestUri.Parameter("api_key", token),
            RequestUri.Parameter("lang", "pl"),
            RequestUri.Parameter("decode", 1))
    };
}

public sealed class Episodes : Request<TitleEpisodes>
{
    private readonly ulong _titleId;

    public Episodes(ulong titleId) => _titleId = titleId;

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, $"api/title/{_titleId}/episodes",
            RequestUri.Parameter("api_key", token))
    };
}

public sealed class EpisodeRange : Request<EpisodesRange>
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

public sealed class Recommendations : Request<TitleRecommendations>
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

public sealed class Reviews : Request<TitleReviews>
{
    private readonly ulong _titleId;

    public Reviews(ulong titleId) => _titleId = titleId;

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, $"api/title/{_titleId}/reviews",
            RequestUri.Parameter("api_key", token))
    };
}

public sealed class Relations : Request<TitleRelations>
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

public sealed class Characters : Request<TitleCharacters>
{
    private readonly ulong _titleId;

    public Characters(ulong titleId) => _titleId = titleId;

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, $"api/title/{_titleId}/characters",
            RequestUri.Parameter("api_key", token))
    };
}
