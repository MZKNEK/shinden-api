using System;
using System.Net.Http;
using ShindenAPI.Models;

namespace ShindenAPI.Queries.People;

public sealed class StaffInfo : Request<Models.StaffInfo>
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

public sealed class CharacterInfo : Request<Models.CharacterInfo>
{
    private readonly ulong _characterId;

    public CharacterInfo(ulong characterId) => _characterId = characterId;

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, $"api/character/{_characterId}",
            RequestUri.Parameter("api_key", token),
            RequestUri.Parameter("fav_stats", 1),
            RequestUri.Parameter("relations", 1),
            RequestUri.Parameter("points", 1),
            RequestUri.Parameter("bio", 1),
            RequestUri.Parameter("pictures", 1))
    };
}
