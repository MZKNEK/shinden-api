using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShindenAPI.Credentials;
using ShindenAPI.Queries;

namespace ShindenAPI;

public class Shinden
{
    private const string SHINDEN_URL = "https://shinden.pl/";
    private const string IMAGE_SERVER_URL = "http://cdn.shinden.eu/";
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

    public static string GetUserAvatarUrl(ulong userId) => GetUserAvatarUrl(string.Empty, userId);

    internal static string GetUserAvatarUrl(string imageId, ulong userId)
    {
        if (string.IsNullOrEmpty(imageId) || imageId is "0")
            return USER_DEFAULT_AVATAR;

        return $"{IMAGE_SERVER_URL}cdn1/avatars/225x350/{userId}.jpg?v{imageId}";
    }
}
