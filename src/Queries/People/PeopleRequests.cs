using ShindenAPI.Models;
using System.Net.Http;
using System;
using System.Collections.Generic;

namespace ShindenAPI.Queries.People;

public sealed class CharacterInfoOptions
{
    public bool Biography { get; init; } = true;
    public bool FavouriteStats { get; init; } = true;
    public bool Points { get; init; } = true;
    public bool Relations { get; init; } = true;
    public bool Pictures { get; init; } = true;
    public bool AnimeOnly { get; init; }
    public string Language { get; init; } = "pl";
}

public sealed class CharacterInfo : GetRequest<Models.CharacterInfo>
{
    public CharacterInfo(ulong characterId, CharacterInfoOptions options = null)
        : base($"compat/php-api/character/{characterId}", Parameters(options ?? new CharacterInfoOptions())) { }

    private static KeyValuePair<string, string>[] Parameters(CharacterInfoOptions options) => new[]
    {
        RequestUri.Parameter("bio", options.Biography ? 1 : 0),
        RequestUri.Parameter("fav_stats", options.FavouriteStats ? 1 : 0),
        RequestUri.Parameter("points", options.Points ? 1 : 0),
        RequestUri.Parameter("relations", options.Relations ? 1 : 0),
        RequestUri.Parameter("pictures", options.Pictures ? 1 : 0),
        RequestUri.Parameter("table", options.AnimeOnly ? "animes" : null),
        RequestUri.Parameter("lang", options.Language)
    };
}


public sealed class StaffInfo : LegacyRequest<Models.StaffInfo>
{
    private readonly ulong _staffId;

    public StaffInfo(ulong staffId) => _staffId = staffId;

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, $"api/staff/{_staffId}",
            RequestUri.Parameter("api_key", token))
    };
}
