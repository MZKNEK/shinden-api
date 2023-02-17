using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShindenAPI.Credentials;
using ShindenAPI.Queries;

namespace ShindenAPI;

public class Shinden
{
    private const string BIG_IMAGE = "genuine";
    private const string SMALL_IAMGE = "225x350";

    private const string SHINDEN_URL = "https://shinden.pl/";
    private const string IMAGE_SERVER_URL = "http://cdn.shinden.eu/";

    private const string USER_TITLE_AVATAR = $"{IMAGE_SERVER_URL}cdn1/other/placeholders/title/225x350.jpg";
    private const string USER_DEFAULT_AVATAR = $"{IMAGE_SERVER_URL}cdn1/other/placeholders/user/100x100.jpg";

    private RequestManager _manager;

    public Shinden(Func<ConfigBuilder, Config> config, ILogger<Shinden> logger = default!)
        : this(config(new ConfigBuilder()), logger) {}

    public Shinden(Config config, ILogger<Shinden> logger = default!)
        : this(config.Uri, config.Auth, logger) {}

    public Shinden(Uri uri, Auth auth, ILogger<Shinden> logger = default!)
         => _manager = new(uri, auth, logger);

    public Task<ErrorOr<TReturn>> AskAsync<TReturn>(Request<TReturn> query) where TReturn : class
        => _manager.MakeQueryAsync(query);

    public bool IsValidUserSession() => _manager.IsValidUserSession();

    public static string GetUserAvatarUrl(ulong userId) => GetUserAvatarUrl(string.Empty, userId);

    public static string GetUserProfileUrl(ulong userId) => $"{SHINDEN_URL}user/{userId}";

    public static string GetTitleUrl(ulong titleId) => $"{SHINDEN_URL}t/{titleId}";

    public static string GetStaffUrl(ulong id) => $"{SHINDEN_URL}staff/{id}";

    public static string GetCharacterUrl(ulong id) => $"{SHINDEN_URL}character/{id}";

    internal static string GetUserAvatarUrl(string imageId, ulong userId)
    {
        if (string.IsNullOrEmpty(imageId) || imageId is "0")
            return USER_DEFAULT_AVATAR;

        return $"{IMAGE_SERVER_URL}cdn1/avatars/225x350/{userId}.jpg?v{imageId}";
    }

    public static string GetPictureUrl(ulong imageId, bool big = true) => GetPictureUrl(imageId.ToString(), big);

    internal static string GetPictureUrl(string imageId, bool big = true)
    {
        if (string.IsNullOrEmpty(imageId) || imageId is "0")
            return USER_TITLE_AVATAR;

        var size = big ? BIG_IMAGE : SMALL_IAMGE;
        return $"{IMAGE_SERVER_URL}cdn1/images/{size}/{imageId}.jpg";
    }
}
