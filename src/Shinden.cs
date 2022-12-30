using System;
using System.Threading.Tasks;
using ShindenAPI.Queries;

namespace ShindenAPI;

public class Shinden
{
    private const string SHINDEN_URL = "https://shinden.pl/";
    private const string IMAGE_SERVER_URL = "http://cdn.shinden.eu/";
    private const string USER_DEFAULT_AVATAR = $"{IMAGE_SERVER_URL}cdn1/other/placeholders/user/100x100.jpg";

    private RequestManager _manager;

    public Shinden(Func<ConfigBuilder, Config> config) : this(config(new ConfigBuilder())) {}
    public Shinden(Config config) : this(config.Uri, config.Auth) {}
    public Shinden(Uri uri, Auth auth) => _manager = new(uri, auth);

    public Task<ErrorOr<TReturn>> AskAsync<TReturn>(Request<TReturn> query) where TReturn : class
        => _manager.MakeQueryAsync(query);

    public static string GetUserAvatarUrl(ulong userId) => GetUserAvatarUrl(string.Empty, userId);

    internal static string GetUserAvatarUrl(string imageId, ulong userId)
    {
        if (string.IsNullOrEmpty(imageId) || imageId is "0")
            return USER_DEFAULT_AVATAR;

        return $"{IMAGE_SERVER_URL}cdn1/avatars/225x350/{userId}.jpg?v{imageId}";
    }
}
