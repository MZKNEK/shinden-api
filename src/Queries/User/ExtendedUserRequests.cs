using ShindenAPI.Models;
using System.Net.Http;
using System;
using System.Collections.Generic;

namespace ShindenAPI.Queries.User;

public sealed class UserInfoOptions
{
    public ulong? Id { get; init; }
    public ulong? ForumId { get; init; }
    public string Name { get; init; }
    public string Email { get; init; }
}

public sealed class UserInfo : GetRequest<Models.UserInfo>
{
    public UserInfo(ulong userId, UserInfoOptions options = null)
        : base($"compat/php-api/user/{userId}/info", RequestUri.Parameter("id", options?.Id),
            RequestUri.Parameter("vb", options?.ForumId), RequestUri.Parameter("name", options?.Name),
            RequestUri.Parameter("email", options?.Email)) { }
}

public sealed class LastWatched : GetRequest<List<Models.LastWatched>>
{
    public LastWatched(ulong userId, uint limit = 5)
        : base($"compat/php-api/user/{userId}/last_view", HistoryLimit.Parameter(limit)) { }
}

public sealed class LastRead : GetRequest<List<Models.LastRead>>
{
    public LastRead(ulong userId, uint limit = 5)
        : base($"compat/php-api/user/{userId}/last_read", HistoryLimit.Parameter(limit)) { }
}

internal static class HistoryLimit
{
    internal static KeyValuePair<string, string> Parameter(uint limit) => RequestUri.Parameter("limit",
        limit is >= 1 and <= 100 ? limit : throw new ArgumentOutOfRangeException(nameof(limit)));
}


public sealed class FavouriteCharacters : LegacyRequest<List<Models.FavouriteCharacter>>
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

public sealed class ChangeTitleStatus : LegacyRequest<TitleStatusAfterChange>
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

public sealed class RemoveTitleStatus : LegacyRequest<TitleStatusAfterChange>
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

public sealed class IncreaseWatchedEpisode : LegacyRequest<IncreaseWatchedResult>
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

public sealed class RateAnime : LegacyRequest<Status>
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

public sealed class RateManga : LegacyRequest<Status>
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
