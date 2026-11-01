using System.Net.Http;
using System;
using System.Collections.Generic;
using ShindenAPI.Models;

namespace ShindenAPI.Queries.Character;

public sealed class FlatList : GetRequest<List<CharacterFlat>>
{
    public FlatList() : base("compat/php-api/character/flat-list") { }
}

public sealed class AnimeFlatList : GetRequest<List<CharacterFlat>>
{
    public AnimeFlatList() : base("compat/php-api/character/flat-list-anime") { }
}


public sealed class ConnectedIds : LegacyRequest<List<ulong>>
{
    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, "api/character/connected",
            RequestUri.Parameter("api_key", token))
    };
}

public sealed class AnimeIds : LegacyRequest<List<ulong>>
{
    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, "api/character/in-anime",
            RequestUri.Parameter("api_key", token))
    };
}

public sealed class MangaIds : LegacyRequest<List<ulong>>
{
    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, "api/character/in-manga",
            RequestUri.Parameter("api_key", token))
    };
}

public sealed class MangaFlatList : LegacyRequest<List<CharacterFlat>>
{
    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, "api/character/flat-list-book",
            RequestUri.Parameter("api_key", token))
    };
}
