using System;
using System.Collections.Generic;
using System.Net.Http;
using ShindenAPI.Models;

namespace ShindenAPI.Queries.Character;

public sealed class ConnectedIds : Request<List<ulong>>
{
    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, "api/character/connected",
            RequestUri.Parameter("api_key", token))
    };
}

public sealed class AnimeIds : Request<List<ulong>>
{
    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, "api/character/in-anime",
            RequestUri.Parameter("api_key", token))
    };
}

public sealed class MangaIds : Request<List<ulong>>
{
    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, "api/character/in-manga",
            RequestUri.Parameter("api_key", token))
    };
}

public sealed class FlatList : Request<List<CharacterFlat>>
{
    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, "api/character/flat-list",
            RequestUri.Parameter("api_key", token))
    };
}

public sealed class AnimeFlatList : Request<List<CharacterFlat>>
{
    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, "api/character/flat-list-anime",
            RequestUri.Parameter("api_key", token))
    };
}

public sealed class MangaFlatList : Request<List<CharacterFlat>>
{
    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, "api/character/flat-list-book",
            RequestUri.Parameter("api_key", token))
    };
}
