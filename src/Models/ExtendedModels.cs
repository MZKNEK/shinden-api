using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ShindenAPI.Models;

/// <summary>Compatibility rows retain undocumented database columns without losing their values.</summary>
public class PhpRow
{
    [JsonExtensionData] public IDictionary<string, JToken> AdditionalData { get; set; } = new Dictionary<string, JToken>();
}

public class AnimeMangaInfo
{
    [JsonProperty("title", Required = Required.Always)] public TitleDetails Title { get; init; }
}

public class TitleDetails : PhpRow
{
    [JsonProperty("title_id", Required = Required.Always)] public ulong Id { get; init; }
    [JsonProperty("title")] public string Name { get; init; }
    [JsonProperty("type")] public string Type { get; init; }
    [JsonProperty("title_status")] public string Status { get; init; }
    [JsonProperty("dmca")] public string Dmca { get; init; }
    [JsonProperty("cover_artifact_id")] public ulong? CoverId { get; init; }
    [JsonProperty("mpaa_rating")] public string MpaaRating { get; init; }
    [JsonProperty("add_date")] public string AddDate { get; init; }
    [JsonProperty("premiere_date")] public string StartDate { get; init; }
    [JsonProperty("finish_date")] public string FinishDate { get; init; }
    [JsonProperty("premiere_string")] public string PremiereString { get; init; }
    [JsonProperty("finish_string")] public string FinishString { get; init; }
    [JsonProperty("anime")] public AnimeDetails Anime { get; init; }
    [JsonProperty("manga")] public MangaDetails Manga { get; init; }
    [JsonProperty("description")] public Description Description { get; init; }
    // PHP sends [] for empty groups, and an object for populated groups.
    [JsonProperty("tags")] public JToken Tags { get; init; }
    [JsonProperty("title_other")] public List<PhpRow> AlternativeTitles { get; init; }
}

public class AnimeDetails : PhpRow
{
    [JsonProperty("anime_type")] public string Type { get; init; }
    [JsonProperty("episodes")] public long? EpisodesCount { get; init; }
    [JsonProperty("episode_time")] public long? EpisodeTime { get; init; }
}

public class MangaDetails : PhpRow
{
    [JsonProperty("chapters")] public long? ChaptersCount { get; init; }
    [JsonProperty("volumes")] public long? VolumesCount { get; init; }
}

public class Description : PhpRow
{
    [JsonProperty("description")] public string Text { get; init; }
    [JsonProperty("lang_code")] public string Language { get; init; }
}

public class TitleEpisodes
{
    [JsonProperty("episodes")] public List<Episode> Episodes { get; init; } = new();
    [JsonProperty("connected_episodes")] public List<Episode> ConnectedEpisodes { get; init; } = new();
}

public class Episode : PhpRow
{
    [JsonProperty("episode_id")] public ulong Id { get; init; }
    [JsonProperty("title_id")] public ulong? TitleId { get; init; }
    [JsonProperty("episode_no")] public long? EpisodeNumber { get; init; }
}

public class NewEpisode : PhpRow
{
    [JsonProperty("episode_id")] public ulong Id { get; init; }
    [JsonProperty("anime_id")] public ulong? AnimeId { get; init; }
    [JsonProperty("title_id")] public ulong? TitleId { get; init; }
}

public class LastWatched : PhpRow
{
    [JsonProperty("view_cnt")] public long? ViewCount { get; init; }
    [JsonProperty("title_id")] public ulong TitleId { get; init; }
    [JsonProperty("episode_id")] public ulong EpisodeId { get; init; }
    [JsonProperty("episode_no")] public long? EpisodeNumber { get; init; }
    [JsonProperty("episodes")] public long? EpisodesCount { get; init; }
    [JsonProperty("title")] public string Title { get; init; }
    [JsonProperty("created_time")] public string CreatedTime { get; init; }
}

public class LastRead : PhpRow
{
    [JsonProperty("view_cnt")] public long? ViewCount { get; init; }
    [JsonProperty("title_id")] public ulong TitleId { get; init; }
    [JsonProperty("chapter_id")] public ulong? ChapterId { get; init; }
    // Split chapters are numbered like "19.1" or "64.5".
    [JsonProperty("chapter_no")] public decimal? ChapterNumber { get; init; }
    [JsonProperty("volume_no")] public long? VolumeNumber { get; init; }
    [JsonProperty("title")] public string Title { get; init; }
    [JsonProperty("created_time")] public string CreatedTime { get; init; }
}

public class CharacterInfo : PhpRow
{
    [JsonProperty("character_id", Required = Required.Always)] public ulong Id { get; init; }
    [JsonProperty("first_name")] public string FirstName { get; init; }
    [JsonProperty("last_name")] public string LastName { get; init; }
    [JsonProperty("is_real")] public string IsReal { get; init; }
    [JsonProperty("picture_artifact_id")] public ulong? PictureId { get; init; }
    [JsonProperty("birth_day")] public string BirthDate { get; init; }
    [JsonProperty("death_day")] public string DeathDate { get; init; }
    [JsonProperty("age")] public string Age { get; init; }
    [JsonProperty("gender")] public string Gender { get; init; }
    [JsonProperty("bloodtype")] public string Bloodtype { get; init; }
    [JsonProperty("height")] public string Height { get; init; }
    [JsonProperty("weight")] public string Weight { get; init; }
    [JsonProperty("bust")] public string Bust { get; init; }
    [JsonProperty("waist")] public string Waist { get; init; }
    [JsonProperty("hips")] public string Hips { get; init; }
    [JsonProperty("bio")] public Biography Biography { get; init; }
    [JsonProperty("fav_stats")] public PhpRow FavouriteStats { get; init; }
    [JsonProperty("points")] public List<PhpRow> Points { get; init; }
    [JsonProperty("relations")] public List<PhpRow> Relations { get; init; }
    [JsonProperty("pictures")] public List<PhpRow> Pictures { get; init; }
}

public class Biography : PhpRow
{
    [JsonProperty("biography")] public string Text { get; init; }
    [JsonProperty("text")] private string LegacyText { init => Text = value; }
    [JsonProperty("content")] public string Content { get; init; }
    [JsonProperty("lang")] public string Language { get; init; }
}

public class UserInfo : PhpRow
{
    [JsonProperty("user_id", Required = Required.Always)] public ulong Id { get; init; }
    [JsonProperty("vb_id")] public ulong? ForumId { get; init; }
    [JsonProperty("name")] public string Name { get; init; }
    [JsonProperty("rank")] public string Rank { get; init; }
    [JsonProperty("email")] public string Email { get; init; }
    [JsonProperty("birthdate")] public string BirthDate { get; init; }
    [JsonProperty("sex")] public string Sex { get; init; }
    [JsonProperty("skin_id")] public ulong? SkinId { get; init; }
    [JsonProperty("about_me")] public string AboutMe { get; init; }
    [JsonProperty("avatar")] public string Avatar { get; init; }
    [JsonProperty("gravatar")] public string Gravatar { get; init; }
    [JsonProperty("anime_css")] public string AnimeCss { get; init; }
    [JsonProperty("manga_css")] public string MangaCss { get; init; }
    [JsonProperty("logo_img_id")] public ulong? LogoImageId { get; init; }
    [JsonProperty("signature")] public string Signature { get; init; }
    [JsonProperty("total_points")] public long? TotalPoints { get; init; }
    [JsonProperty("status")] public string Status { get; init; }
    [JsonProperty("register_date")] public string RegisterDate { get; init; }
    [JsonProperty("portal_lang")] public string PortalLanguage { get; init; }
    [JsonProperty("last_active")] public string LastTimeActive { get; init; }
    [JsonProperty("watched_status")] public Dictionary<string, long?> WatchedStatus { get; init; }
    [JsonProperty("watched_cnt")] public long? WatchedCount { get; init; }
    [JsonProperty("watch_time")] public DedicatedTime WatchTime { get; init; }
    [JsonProperty("mean_anime_score")] public MeanScore MeanAnimeScore { get; init; }
    [JsonProperty("readed_status")] public Dictionary<string, long?> ReadStatus { get; init; }
    [JsonProperty("chapters_cnt")] public long? ChaptersCount { get; init; }
    [JsonProperty("read_time")] public DedicatedTime ReadTime { get; init; }
    [JsonProperty("mean_manga_score")] public MeanScore MeanMangaScore { get; init; }
    [JsonProperty("read_speed")] public ReadSpeed ReadSpeed { get; init; }
}

public class DedicatedTime
{
    [JsonProperty("y")] public long Years { get; init; }
    [JsonProperty("n")] public long DaysInYear { get; init; }
    [JsonProperty("d")] public long Days { get; init; }
    [JsonProperty("h")] public long Hours { get; init; }
    [JsonProperty("m")] public long Minutes { get; init; }
    [JsonProperty("t")] public long? TotalMinutes { get; init; }
}

public class MeanScore
{
    [JsonProperty("mean_score")] public double? Rating { get; init; }
    [JsonProperty("scores_cnt")] public long Count { get; init; }
}

public class ReadSpeed : PhpRow
{
    [JsonProperty("manga_read_time")] public double? MangaReadTime { get; init; }
    [JsonProperty("vn_read_time")] public double? VisualNovelReadTime { get; init; }
    [JsonProperty("manga_proc")] public double? MangaProgress { get; init; }
    [JsonProperty("vn_proc")] public double? VisualNovelProgress { get; init; }
}

public class SeasonalAnimeInfo
{
    /// <summary>Year and season (winter/spring/summer/fall) the lists belong to; same shape as the v1 title season.</summary>
    [JsonProperty("season")] public TitleByIdSeason Season { get; init; }
    /// <summary>When the lists last changed, as sent by the API (server time, no offset).</summary>
    [JsonProperty("updated_at")] public DateTime? UpdatedAt { get; init; }
    [JsonProperty("titles")] public List<ulong> TitleIds { get; init; } = new();
    [JsonProperty("characters")] public List<ulong> CharacterIds { get; init; } = new();
    [JsonProperty("character_titles")] public List<SeasonalCharacterTitle> CharacterTitles { get; init; } = new();
}

public class SeasonalCharacterTitle
{
    [JsonProperty("character_id")] public ulong CharacterId { get; init; }
    [JsonProperty("title_id")] public ulong TitleId { get; init; }
}

public class CharacterFlat
{
    [JsonProperty("c")] public ulong CharacterId { get; init; }
    [JsonProperty("p")] public ulong PictureId { get; init; }
    [JsonProperty("f")] public string FirstName { get; init; }
    [JsonProperty("l")] public string LastName { get; init; }
    [JsonProperty("t")] public string TitleName { get; init; }
    [JsonProperty("n")] public long RankingPosition { get; init; }
}

public class TitleCharacters
{
    [JsonProperty("relations")] public List<PhpRow> Relations { get; init; }
}
