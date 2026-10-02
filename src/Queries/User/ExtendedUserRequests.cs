using System;
using System.Collections.Generic;
using System.Net.Http;
using ShindenAPI.Models;

namespace ShindenAPI.Queries.User;

public sealed class UserInfo : Request<Models.UserInfo>
{
    private readonly ulong _userId;

    public UserInfo(ulong userId) => _userId = userId;

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, $"api/user/{_userId}/info",
            RequestUri.Parameter("api_key", token))
    };
}

public sealed class LastWatched : Request<List<Models.LastWatched>>
{
    private readonly ulong _userId;
    private readonly uint _limit;

    public LastWatched(ulong userId, uint limit = 5)
    {
        _userId = userId;
        _limit = limit;
    }

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, $"api/user/{_userId}/last_view",
            RequestUri.Parameter("api_key", token),
            RequestUri.Parameter("limit", _limit))
    };
}

public sealed class LastRead : Request<List<Models.LastRead>>
{
    private readonly ulong _userId;
    private readonly uint _limit;

    public LastRead(ulong userId, uint limit = 5)
    {
        _userId = userId;
        _limit = limit;
    }

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, $"api/user/{_userId}/last_read",
            RequestUri.Parameter("api_key", token),
            RequestUri.Parameter("limit", _limit))
    };
}

public sealed class FavouriteCharacters : Request<List<Models.FavouriteCharacter>>
{
    private readonly ulong _userId;

    public FavouriteCharacters(ulong userId) => _userId = userId;

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Get,
        RequestUri = RequestUri.Create(uri, $"api/user/{_userId}/fav-chars",
            RequestUri.Parameter("api_key", token))
    };
}

public sealed class ChangeTitleStatus : Request<TitleStatusAfterChange>
{
    private readonly ulong _userId;
    private readonly ulong _titleId;
    private readonly string _status;

    public ChangeTitleStatus(ulong userId, ulong titleId, string status)
    {
        _userId = userId;
        _titleId = titleId;
        _status = status;
    }

    public ChangeTitleStatus(ulong userId, ulong titleId, TitleListStatus status)
        : this(userId, titleId, status.ToApiValue()) { }

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Post,
        RequestUri = RequestUri.Create(uri, $"api/userlist/{_userId}/series/{_titleId}",
            RequestUri.Parameter("api_key", token)),
        Content = RequestUri.Form(new KeyValuePair<string, string>("status", _status))
    };

    internal override bool RequireHttps() => true;
}

public sealed class RemoveTitleStatus : Request<TitleStatusAfterChange>
{
    private readonly ulong _userId;
    private readonly ulong _titleId;

    public RemoveTitleStatus(ulong userId, ulong titleId)
    {
        _userId = userId;
        _titleId = titleId;
    }

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Delete,
        RequestUri = RequestUri.Create(uri, $"api/userlist/{_userId}/series/{_titleId}",
            RequestUri.Parameter("api_key", token))
    };

    internal override bool RequireHttps() => true;
}

public sealed class IncreaseWatchedEpisode : Request<IncreaseWatchedResult>
{
    private readonly ulong _userId;
    private readonly ulong _titleId;

    public IncreaseWatchedEpisode(ulong userId, ulong titleId)
    {
        _userId = userId;
        _titleId = titleId;
    }

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Post,
        RequestUri = RequestUri.Create(uri, $"api/userlist/{_userId}/increase-watched/{_titleId}",
            RequestUri.Parameter("api_key", token))
    };

    internal override bool RequireHttps() => true;
}

public sealed class RateAnime : Request<Status>
{
    private readonly ulong _titleId;
    private readonly string _type;
    private readonly uint _value;

    public RateAnime(ulong titleId, string type, uint value)
    {
        _titleId = titleId;
        _type = type;
        _value = value;
    }

    public RateAnime(ulong titleId, AnimeRatingCategory type, uint value)
        : this(titleId, type.ToApiValue(), value) { }

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Post,
        RequestUri = RequestUri.Create(uri, $"api/anime/{_titleId}/rate",
            RequestUri.Parameter("api_key", token)),
        Content = RequestUri.Form(
            new KeyValuePair<string, string>("type", _type),
            RequestUri.Parameter("value", _value))
    };

    internal override bool RequireHttps() => true;
}

public sealed class RateManga : Request<Status>
{
    private readonly ulong _titleId;
    private readonly string _type;
    private readonly uint _value;

    public RateManga(ulong titleId, string type, uint value)
    {
        _titleId = titleId;
        _type = type;
        _value = value;
    }

    public RateManga(ulong titleId, MangaRatingCategory type, uint value)
        : this(titleId, type.ToApiValue(), value) { }

    internal override HttpRequestMessage Build(Uri uri, string token) => new()
    {
        Method = HttpMethod.Post,
        RequestUri = RequestUri.Create(uri, $"api/manga/{_titleId}/rate",
            RequestUri.Parameter("api_key", token)),
        Content = RequestUri.Form(
            new KeyValuePair<string, string>("type", _type),
            RequestUri.Parameter("value", _value))
    };

    internal override bool RequireHttps() => true;
}

internal static class ActionValues
{
    internal static string ToApiValue(this TitleListStatus status) => status switch
    {
        TitleListStatus.Completed => "completed",
        TitleListStatus.Dropped => "dropped",
        TitleListStatus.OnHold => "hold",
        TitleListStatus.Plan => "plan",
        TitleListStatus.Skip => "skip",
        _ => "in progress"
    };

    internal static string ToApiValue(this AnimeRatingCategory category) => category switch
    {
        AnimeRatingCategory.Story => "story",
        AnimeRatingCategory.Characters => "titlecahracters",
        AnimeRatingCategory.Graphics => "graphics",
        AnimeRatingCategory.Music => "music",
        _ => "total"
    };

    internal static string ToApiValue(this MangaRatingCategory category) => category switch
    {
        MangaRatingCategory.Story => "story",
        MangaRatingCategory.Characters => "titlecahracters",
        MangaRatingCategory.Art => "lines",
        _ => "total"
    };
}
