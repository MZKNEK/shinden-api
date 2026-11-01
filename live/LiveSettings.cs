using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Diagnostics.CodeAnalysis;
using ShindenAPI.Credentials;
using ShindenAPI.Queries.V1;

namespace ShindenAPI.LiveTests;

internal sealed class SettingsException : Exception
{
    internal SettingsException(string message) : base(message) { }
}

internal sealed class LiveSettings
{
    public string UserAgent { get; set; } = "Shinden.NET.LiveTests/0.7";
    public int RequestTimeoutSeconds { get; set; } = 60;
    public int RequestSpacingMilliseconds { get; set; } = 750;
    public NewApiSettings NewApi { get; set; } = new();
    public LegacyApiSettings LegacyApi { get; set; } = new();
    public ResourceSettings Data { get; set; } = new();
    public bool IncludeBulkCharacterLists { get; set; }
    public bool IncludePocketWaifu { get; set; } = true;
    public WriteSettings Writes { get; set; } = new();

    internal static LiveSettings Load(string path)
    {
        if (!File.Exists(path))
            throw new SettingsException("Local JSON configuration not found. Copy live-settings.example.json to live-settings.local.json and fill in your settings.");
        try
        {
            return JsonConvert.DeserializeObject<LiveSettings>(File.ReadAllText(path), JsonSettings)
                ?? throw new SettingsException("The JSON configuration is empty.");
        }
        catch (JsonException)
        {
            // JSON parser errors can contain the value of a secret; do not print them.
            throw new SettingsException("Invalid JSON or unknown configuration field. Check field names and types against the template.");
        }
    }

    internal static readonly JsonSerializerSettings JsonSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        MissingMemberHandling = MissingMemberHandling.Error,
        Formatting = Formatting.Indented
    };

    internal void Validate(bool allowWrites)
    {
        Need(NewApi is not null && LegacyApi is not null && Data is not null && Writes is not null, "A configuration section is missing.");
        Need(!string.IsNullOrWhiteSpace(UserAgent), "Set userAgent.");
        Need(RequestTimeoutSeconds is >= 1 and <= 600, "requestTimeoutSeconds must be between 1 and 600.");
        Need(RequestSpacingMilliseconds is >= 500 and <= 60000, "requestSpacingMilliseconds must be between 500 and 60000.");
        ValidateUri(NewApi.BaseUri, "newApi.baseUri");
        Need(NewApi.Authentication is not null, "newApi.authentication is missing.");
        if (NewApi.AuthenticatedTestsEnabled)
            Need(!string.IsNullOrWhiteSpace(NewApi.Authentication.BearerToken), "Set newApi.authentication.bearerToken or disable newApi.authenticatedTestsEnabled.");
        Need(LegacyApi.Authentication is not null && LegacyApi.UserAuthentication is not null, "A legacyApi authentication section is missing.");
        if (LegacyApi.Enabled)
        {
            ValidateUri(LegacyApi.BaseUri, "legacyApi.baseUri");
            Need(!string.IsNullOrWhiteSpace(LegacyApi.Authentication.ApiKey), "Set legacyApi.authentication.apiKey or disable legacyApi.enabled.");
        }
        if (LegacyApi.UserAuthentication.Enabled)
        {
            Need(LegacyApi.Enabled, "PHP login requires legacyApi.enabled.");
            Need(!string.IsNullOrWhiteSpace(LegacyApi.UserAuthentication.Username) && !string.IsNullOrWhiteSpace(LegacyApi.UserAuthentication.Password),
                "Set username and password in legacyApi.userAuthentication.");
        }
        foreach (var query in new[] { Data.UserQuery, Data.AnimeQuery, Data.MangaQuery, Data.CharacterQuery, Data.StaffQuery, Data.TagQuery })
            Need(!string.IsNullOrWhiteSpace(query) && query.Length is >= 2 and <= 200, "Search queries in data must contain between 2 and 200 characters.");
        Need(Data.SeasonYear is >= 1900 and <= 2100, "Invalid data.seasonYear.");
        Need(Enum.TryParse<Season>(Data.Season, true, out var season) && Enum.IsDefined(typeof(Season), season), "data.season must be Winter, Spring, Summer or Autumn.");

        if (!Writes.Enabled)
        {
            Need(!allowWrites, "The --allow-writes flag requires writes.enabled=true in the JSON configuration.");
            return;
        }
        Need(allowWrites, "Writes require both writes.enabled=true and the --allow-writes flag.");
        Need(LegacyApi.UserAuthentication.Enabled, "Writes require PHP login to be enabled.");
        Need(Writes.ExpectedUserId > 0, "Set writes.expectedUserId to the test account ID.");
        Need(Writes.Favourite is not null && Writes.TitleStatus is not null && Writes.AnimeRating is not null && Writes.MangaRating is not null && Writes.IncreaseWatched is not null,
            "A scenario is missing from the writes section.");
        Need(Writes.Favourite.Enabled || Writes.TitleStatus.Enabled || Writes.AnimeRating.Enabled || Writes.MangaRating.Enabled || Writes.IncreaseWatched.Enabled,
            "Enable at least one write scenario.");
        if (Writes.Favourite.Enabled)
        {
            Need(Writes.Favourite.ObjectId > 0 && Writes.Favourite.InitiallyFavourite.HasValue, "Set writes.favourite.objectId and initiallyFavourite.");
            Need(new[] { "character", "title", "staff" }.Contains(Writes.Favourite.Type), "writes.favourite.type must be character, title or staff.");
        }
        if (Writes.TitleStatus.Enabled)
        {
            Need(Writes.TitleStatus.TitleId > 0 && Writes.TitleStatus.InitialStateConfirmed, "Set writes.titleStatus.titleId and confirm initialStateConfirmed.");
            var statuses = new[] { "in progress", "completed", "dropped", "hold", "plan", "skip" };
            Need(statuses.Contains(Writes.TitleStatus.TestStatus), "Invalid writes.titleStatus.testStatus.");
            Need(Writes.TitleStatus.InitialStatus is null || statuses.Contains(Writes.TitleStatus.InitialStatus), "Invalid writes.titleStatus.initialStatus; null means no list entry.");
        }
        ValidateRating(Writes.AnimeRating, "writes.animeRating", new[] { "total", "story", "titlecahracters", "graphics", "music" });
        ValidateRating(Writes.MangaRating, "writes.mangaRating", new[] { "total", "story", "titlecahracters", "lines" });
        if (Writes.IncreaseWatched.Enabled)
            Need(Writes.IncreaseWatched.TitleId > 0, "Set writes.increaseWatched.titleId.");
    }

    private static void ValidateRating(RatingWrite settings, string field, string[] categories)
    {
        if (!settings.Enabled) return;
        Need(settings.TitleId > 0 && settings.TestValue is >= 1 and <= 10 && settings.RestoreValue is >= 1 and <= 10,
            $"Set {field}: titleId, testValue and restoreValue (ratings 1–10; use a title that already has a rating).");
        Need(categories.Contains(settings.Category), $"Invalid {field}.category.");
    }

    private static void ValidateUri(string value, string field)
    {
        Need(Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment),
            $"{field} must be an HTTPS address without credentials, query parameters or a fragment.");
    }

    private static void Need([DoesNotReturnIf(false)] bool valid, string message) { if (!valid) throw new SettingsException(message); }

    internal Shinden CreateClient(bool guest = false, bool login = false, HttpMessageHandler? handler = null)
    {
        return new Shinden(builder =>
        {
            builder.WithUri(NewApi.BaseUri).WithAuth(new Auth
                { Token = guest || !NewApi.AuthenticatedTestsEnabled ? null! : NewApi.Authentication.BearerToken, UserAgent = UserAgent })
                .WithRequestTimeout(TimeSpan.FromSeconds(RequestTimeoutSeconds)).WithMaxRetries(0);
            if (LegacyApi.Enabled && !guest)
            {
                var old = LegacyApi.Authentication;
                var user = LegacyApi.UserAuthentication;
                builder.WithLegacyUri(LegacyApi.BaseUri).WithLegacyAuth(login
                    ? new UserAuth { Token = old.ApiKey, UserAgent = UserAgent, Marmolade = old.MarmoladeHeaderName,
                        Nickname = user.Username, Password = user.Password }
                    : new Auth { Token = old.ApiKey, UserAgent = UserAgent, Marmolade = old.MarmoladeHeaderName });
            }
            return builder.Build();
        }, handler: handler!);
    }
}

internal sealed class NewApiSettings
{
    public string BaseUri { get; set; } = "https://shinden.pl/";
    public bool AuthenticatedTestsEnabled { get; set; } = true;
    public BearerSettings Authentication { get; set; } = new();
}
internal sealed class BearerSettings { public string BearerToken { get; set; } = ""; }
internal sealed class LegacyApiSettings
{
    public bool Enabled { get; set; } = true;
    public string BaseUri { get; set; } = "https://shinden.pl/";
    public PhpKeySettings Authentication { get; set; } = new();
    public PhpUserSettings UserAuthentication { get; set; } = new();
}
internal sealed class PhpKeySettings
{
    public string ApiKey { get; set; } = "";
    public string MarmoladeHeaderName { get; set; } = "";
}
internal sealed class PhpUserSettings
{
    public bool Enabled { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public ulong ExpectedUserId { get; set; }
}
internal sealed class ResourceSettings
{
    public ulong UserId { get; set; }
    public ulong AnimeId { get; set; }
    public ulong MangaId { get; set; }
    public ulong CharacterId { get; set; }
    public ulong StaffId { get; set; }
    public ulong PocketWaifuUserId { get; set; }
    public ulong CardId { get; set; }
    public string UserQuery { get; set; } = "Sniku";
    public string AnimeQuery { get; set; } = "Gintama";
    public string MangaQuery { get; set; } = "Dorohedoro";
    public string CharacterQuery { get; set; } = "Gintoki";
    public string StaffQuery { get; set; } = "Marina";
    public string TagQuery { get; set; } = "akcja";
    public int SeasonYear { get; set; } = 2026;
    public string Season { get; set; } = "Autumn";
}
internal sealed class WriteSettings
{
    public bool Enabled { get; set; }
    public ulong ExpectedUserId { get; set; }
    public FavouriteWrite Favourite { get; set; } = new();
    public StatusWrite TitleStatus { get; set; } = new();
    public RatingWrite AnimeRating { get; set; } = new();
    public RatingWrite MangaRating { get; set; } = new();
    public IncreaseWrite IncreaseWatched { get; set; } = new();
}
internal sealed class FavouriteWrite
{
    public bool Enabled { get; set; }
    public string Type { get; set; } = "character";
    public ulong ObjectId { get; set; }
    public bool? InitiallyFavourite { get; set; }
}
internal sealed class StatusWrite
{
    public bool Enabled { get; set; }
    public ulong TitleId { get; set; }
    public string? InitialStatus { get; set; }
    public bool InitialStateConfirmed { get; set; }
    public string TestStatus { get; set; } = "hold";
}
internal sealed class RatingWrite
{
    public bool Enabled { get; set; }
    public ulong TitleId { get; set; }
    public string Category { get; set; } = "total";
    public uint TestValue { get; set; } = 8;
    public uint? RestoreValue { get; set; }
}
internal sealed class IncreaseWrite
{
    public bool Enabled { get; set; }
    public ulong TitleId { get; set; }
}
