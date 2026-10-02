using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ShindenAPI.Models;

public class AnimeMangaInfo
{
    [JsonProperty("title")] public TitleDetails Title { get; init; }
}

public class TitleDetails
{
    [JsonProperty("title_id")] public ulong? Id { get; init; }
    [JsonProperty("title")] public string Name { get; init; }
    [JsonProperty("type")] public string Type { get; init; }
    [JsonProperty("title_status")] public string Status { get; init; }
    [JsonProperty("dmca")] public string Dmca { get; init; }
    [JsonProperty("cover_artifact_id")] public string CoverId { get; init; }
    [JsonProperty("mpaa_rating")] public string MpaaRating { get; init; }
    [JsonProperty("add_date")] public string AddDate { get; init; }
    [JsonProperty("premiere_date")] public string StartDate { get; init; }
    [JsonProperty("finish_date")] public string FinishDate { get; init; }
    [JsonProperty("episode_time")] public string EpisodeTime { get; init; }
    [JsonProperty("anime")] public AnimeDetails Anime { get; init; }
    [JsonProperty("manga")] public MangaDetails Manga { get; init; }
    [JsonProperty("description")] public Description Description { get; init; }
    [JsonProperty("tags")] public TitleTags Tags { get; init; }
    [JsonProperty("title_other")] public List<AlternativeTitle> AlternativeTitles { get; init; }
}

public class AnimeDetails
{
    [JsonProperty("anime_type")] public string Type { get; init; }
    [JsonProperty("episodes")] public string EpisodesCount { get; init; }
    [JsonProperty("episode_time")] public string EpisodeTime { get; init; }
}

public class MangaDetails
{
    [JsonProperty("chapters")] public string ChaptersCount { get; init; }
    [JsonProperty("volumes")] public string VolumesCount { get; init; }
}

public class TitleTags
{
    [JsonProperty("genre")] public TagGroup Genre { get; init; }
    [JsonProperty("source")] public TagGroup Source { get; init; }
    [JsonProperty("entity")] public TagGroup Entity { get; init; }
    [JsonProperty("place")] public TagGroup Place { get; init; }
    [JsonProperty("studio")] public TagGroup Studio { get; init; }
    [JsonProperty("tag")] public TagGroup Tag { get; init; }
    [JsonProperty("production_type")] public TagGroup ProductionType { get; init; }
}

public class TagGroup
{
    [JsonProperty("name")] public string Name { get; init; }
    [JsonProperty("items")] public List<Tag> Items { get; init; }
}

public class Description
{
    [JsonProperty("text")] public string Text { get; init; }
    [JsonProperty("content")] public string Content { get; init; }
    [JsonProperty("lang")] public string Language { get; init; }
}

public class AlternativeTitle
{
    [JsonProperty("title")] public string Title { get; init; }
    [JsonProperty("type")] public string Type { get; init; }
    [JsonProperty("language")] public string Language { get; init; }
}

public class TagCategory
{
    [JsonProperty("name")] public string Name { get; init; }
    [JsonProperty("category")] public string Category { get; init; }
    [JsonProperty("tags")] public List<Tag> Tags { get; init; }
}

public class Tag
{
    [JsonProperty("tag_id")] public ulong? Id { get; init; }
    [JsonProperty("tag_name")] public string Name { get; init; }
    [JsonProperty("name")] public string DisplayName { get; init; }
}

public class Episode
{
    [JsonProperty("id")] public ulong Id { get; init; }
    [JsonProperty("episode_id")] public ulong? EpisodeId { get; init; }
    [JsonProperty("anime_id")] public ulong AnimeId { get; init; }
    [JsonProperty("is_filler")] public bool IsFiller { get; init; }
    [JsonProperty("has_online")] public bool HasOnline { get; init; }
    [JsonProperty("online_id")] public ulong? OnlineId { get; init; }
    [JsonProperty("type")] public string Type { get; init; }
    [JsonProperty("air_date")] public string AirDate { get; init; }
    [JsonProperty("air_channel")] public string AirChannel { get; init; }
    [JsonProperty("episode_number")] public ulong EpisodeNumber { get; init; }
    [JsonProperty("episode_length")] public string EpisodeLength { get; init; }
    [JsonProperty("episode_title")] public AlternativeTitle EpisodeTitle { get; init; }
}

public class EpisodesRange
{
    [JsonProperty("min_no")] public string Min { get; init; }
    [JsonProperty("max_no")] public string Max { get; init; }
    [JsonProperty("status")] public string Status { get; init; }
}

public class TitleEpisodes
{
    [JsonProperty("episodes")] public List<Episode> Episodes { get; init; }
    [JsonProperty("connected_episodes")] public List<Episode> ConnectedEpisodes { get; init; }
}

public class TitleRecommendations
{
    [JsonProperty("recommendations")] public List<Recommendation> Recommendations { get; init; }
}

public class NewEpisode
{
    [JsonProperty("episode_id")] public ulong Id { get; init; }
    [JsonProperty("anime_id")] public ulong AnimeId { get; init; }
    [JsonProperty("cover_id")] public ulong? CoverId { get; init; }
    [JsonProperty("add_date")] public string AddDate { get; init; }
    [JsonProperty("anime_title")] public string AnimeTitle { get; init; }
    [JsonProperty("episode_number")] public ulong EpisodeNumber { get; init; }
    [JsonProperty("episode_length")] public string EpisodeLength { get; init; }
    [JsonProperty("subtitles_language")] public string SubtitlesLanguage { get; init; }
}

public class LastWatched
{
    [JsonProperty("view_cnt")] public uint ViewCount { get; init; }
    [JsonProperty("is_filler")] public bool IsFiller { get; init; }
    [JsonProperty("title_id")] public ulong TitleId { get; init; }
    [JsonProperty("is_special")] public bool IsSpecial { get; init; }
    [JsonProperty("episode_no")] public long EpisodeNumber { get; init; }
    [JsonProperty("type")] public string Type { get; init; }
    [JsonProperty("episode_id")] public ulong EpisodeId { get; init; }
    [JsonProperty("episodes_cnt")] public long EpisodesCount { get; init; }
    [JsonProperty("anime_title")] public string AnimeTitle { get; init; }
    [JsonProperty("title_cover_id")] public ulong? CoverId { get; init; }
    [JsonProperty("episode_title")] public string EpisodeTitle { get; init; }
    [JsonProperty("watched_date")] public string WatchedDate { get; init; }
}

public class LastRead
{
    [JsonProperty("view_cnt")] public uint ViewCount { get; init; }
    [JsonProperty("title_id")] public ulong TitleId { get; init; }
    [JsonProperty("volume_no")] public long VolumeNumber { get; init; }
    [JsonProperty("type")] public string Type { get; init; }
    [JsonProperty("chapter_no")] public long ChapterNumber { get; init; }
    [JsonProperty("chapter_id")] public ulong ChapterId { get; init; }
    [JsonProperty("volumes_cnt")] public long VolumesCount { get; init; }
    [JsonProperty("chapters_cnt")] public long ChaptersCount { get; init; }
    [JsonProperty("manga_title")] public string MangaTitle { get; init; }
    [JsonProperty("chapter_title")] public string ChapterTitle { get; init; }
    [JsonProperty("readed_date")] public string ReadedDate { get; init; }
    [JsonProperty("title_cover_id")] public ulong? CoverId { get; init; }
}

public class Recommendation
{
    [JsonProperty("id")] public ulong Id { get; init; }
    [JsonProperty("rating")] public long Rating { get; init; }
    [JsonProperty("rate_count")] public long RateCount { get; init; }
    [JsonProperty("content")] public string Content { get; init; }
    [JsonProperty("add_date")] public string AddDate { get; init; }
    [JsonProperty("base_title_id")] public ulong BaseTitleId { get; init; }
    [JsonProperty("author")] public SimpleUser Author { get; init; }
    [JsonProperty("recommended_title")] public SimpleTitleInfo RecommendedTitle { get; init; }
}

public class Review
{
    [JsonProperty("id")] public ulong Id { get; init; }
    [JsonProperty("rating")] public long Rating { get; init; }
    [JsonProperty("title_id")] public ulong TitleId { get; init; }
    [JsonProperty("rate_count")] public long RateCount { get; init; }
    [JsonProperty("read_count")] public long ReadCount { get; init; }
    [JsonProperty("content")] public string Content { get; init; }
    [JsonProperty("enter_count")] public long EnterCount { get; init; }
    [JsonProperty("is_abstract")] public bool IsAbstract { get; init; }
    [JsonProperty("author")] public SimpleUser Author { get; init; }
}

public class Relation
{
    [JsonProperty("character_first_name")] public string CharacterFirstName { get; init; }
    [JsonProperty("character_last_name")] public string CharacterLastName { get; init; }
    [JsonProperty("staff_first_name")] public string StaffFirstName { get; init; }
    [JsonProperty("staff_last_name")] public string StaffLastName { get; init; }
    [JsonProperty("character_role")] public string CharacterRole { get; init; }
    [JsonProperty("staff_position")] public string StaffPosition { get; init; }
    [JsonProperty("character_id")] public ulong? CharacterId { get; init; }
    [JsonProperty("language")] public string Language { get; init; }
    [JsonProperty("staff_id")] public ulong? StaffId { get; init; }
    [JsonProperty("title")] public string Title { get; init; }
    [JsonProperty("id")] public ulong Id { get; init; }
}

public class TitleRelation
{
    [JsonProperty("title2title_id")] public string Title2TitleId { get; init; }
    [JsonProperty("title_id")] public string TitleId { get; init; }
    [JsonProperty("related_title_id")] public string RelatedTitleId { get; init; }
    [JsonProperty("title")] public string Title { get; init; }
    [JsonProperty("type")] public string Type { get; init; }
    [JsonProperty("relation_type")] public string RelationType { get; init; }
    [JsonProperty("relation_order")] public string RelationOrder { get; init; }
    [JsonProperty("title_status")] public string TitleStatus { get; init; }
    [JsonProperty("cover_artifact_id")] public string CoverArtifactId { get; init; }
    [JsonProperty("ranking_position")] public string RankingPosition { get; init; }
    [JsonProperty("ranking_rate")] public string RankingRate { get; init; }
    [JsonProperty("premiere_date")] public string PremiereDate { get; init; }
    [JsonProperty("finish_date")] public string FinishDate { get; init; }
}

public class SimpleUser
{
    [JsonProperty("user_id")] public ulong Id { get; init; }
    [JsonProperty("name")] public string Name { get; init; }
    [JsonProperty("avatar")] public string Avatar { get; init; }
}

public class SimpleTitleInfo
{
    [JsonProperty("title_id")] public ulong Id { get; init; }
    [JsonProperty("title")] public string Title { get; init; }
    [JsonProperty("cover")] public string CoverId { get; init; }
}

public class CharacterFavs
{
    [JsonProperty("fav_cnt")] public long FavouriteCount { get; init; }
    [JsonProperty("unfav_cnt")] public long UnfavouriteCount { get; init; }
    [JsonProperty("avg_pos")] public double AveragePosition { get; init; }
    [JsonProperty("first_pos_cnt")] public long FirstPositionCount { get; init; }
    [JsonProperty("under3_pos_cnt")] public long Under3PositionCount { get; init; }
    [JsonProperty("under10_pos_cnt")] public long Under10PositionCount { get; init; }
    [JsonProperty("under50_pos_cnt")] public long Under50PositionCount { get; init; }
}

public class CharacterInfo
{
    [JsonProperty("id")] public ulong Id { get; init; }
    [JsonProperty("character_id")] public ulong? CharacterId { get; init; }
    [JsonProperty("gender")] public string Gender { get; init; }
    [JsonProperty("age")] public string Age { get; init; }
    [JsonProperty("hips")] public string Hips { get; init; }
    [JsonProperty("bust")] public string Bust { get; init; }
    [JsonProperty("is_real")] public bool IsReal { get; init; }
    [JsonProperty("waist")] public string Waist { get; init; }
    [JsonProperty("height")] public string Height { get; init; }
    [JsonProperty("weight")] public string Weight { get; init; }
    [JsonProperty("last_name")] public string LastName { get; init; }
    [JsonProperty("first_name")] public string FirstName { get; init; }
    [JsonProperty("bloodtype")] public string Bloodtype { get; init; }
    [JsonProperty("picture")] public ulong? PictureId { get; init; }
    [JsonProperty("birthdate")] public string BirthDate { get; init; }
    [JsonProperty("deathdate")] public string DeathDate { get; init; }
    [JsonProperty("pictures")] public List<Picture> Pictures { get; init; }
    [JsonProperty("points")] public List<EditPoint> Points { get; init; }
    [JsonProperty("favs_stats")] public CharacterFavs FavouriteStats { get; init; }
    [JsonProperty("relations")] public List<Relation> Relations { get; init; }
    [JsonProperty("biography")] public Biography Biography { get; init; }
}

public class CharacterInfoShort
{
    [JsonProperty("character_id")] public ulong Id { get; init; }
    [JsonProperty("last_name")] public string LastName { get; init; }
    [JsonProperty("first_name")] public string FirstName { get; init; }
    [JsonProperty("picture")] public ulong? PictureId { get; init; }
}

public class Picture
{
    [JsonProperty("id")] public ulong? Id { get; init; }
    [JsonProperty("picture_id")] public ulong? PictureId { get; init; }
    [JsonProperty("type")] public string Type { get; init; }
    [JsonProperty("url")] public string Url { get; init; }
}

public class EditPoint
{
    [JsonProperty("id")] public ulong? Id { get; init; }
    [JsonProperty("user_id")] public ulong? UserId { get; init; }
    [JsonProperty("value")] public string Value { get; init; }
}

public class Biography
{
    [JsonProperty("text")] public string Text { get; init; }
    [JsonProperty("content")] public string Content { get; init; }
}

public class StaffInfo
{
    [JsonProperty("id")] public ulong Id { get; init; }
    [JsonProperty("staff_id")] public ulong? StaffId { get; init; }
    [JsonProperty("gender")] public string Gender { get; init; }
    [JsonProperty("last_name")] public string LastName { get; init; }
    [JsonProperty("first_name")] public string FirstName { get; init; }
    [JsonProperty("birth_place")] public string BirthPlace { get; init; }
    [JsonProperty("birthdate")] public string BirthDate { get; init; }
    [JsonProperty("deathdate")] public string DeathDate { get; init; }
    [JsonProperty("staff_type")] public string StaffType { get; init; }
    [JsonProperty("nationality")] public string Nationality { get; init; }
    [JsonProperty("picture")] public ulong? PictureId { get; init; }
    [JsonProperty("biography")] public Biography Biography { get; init; }
    [JsonProperty("relations")] public List<Relation> Relations { get; init; }
}

public class UserInfo
{
    [JsonProperty("user_id")] public ulong Id { get; init; }
    [JsonProperty("name")] public string Name { get; init; }
    [JsonProperty("rank")] public string Rank { get; init; }
    [JsonProperty("email")] public string Email { get; init; }
    [JsonProperty("skin_id")] public ulong? SkinId { get; init; }
    [JsonProperty("forum_id")] public ulong? ForumId { get; init; }
    [JsonProperty("about_me")] public string AboutMe { get; init; }
    [JsonProperty("avatar")] public ulong? AvatarId { get; init; }
    [JsonProperty("anime_css")] public string AnimeCss { get; init; }
    [JsonProperty("manga_css")] public string MangaCss { get; init; }
    [JsonProperty("logo_img_id")] public ulong? LogoImageId { get; init; }
    [JsonProperty("signature")] public string Signature { get; init; }
    [JsonProperty("total_points")] public long? TotalPoints { get; init; }
    [JsonProperty("status")] public string Status { get; init; }
    [JsonProperty("list_stats")] public UserListStats ListStats { get; init; }
    [JsonProperty("register_date")] public string RegisterDate { get; init; }
    [JsonProperty("portal_lang")] public string PortalLanguage { get; init; }
    [JsonProperty("last_active")] public string LastTimeActive { get; init; }
}

public class UserListStats
{
    [JsonProperty("read_speed")] public ReadSpeed ReadSpeed { get; init; }
    [JsonProperty("chapters_count")] public ulong? ChaptersCount { get; init; }
    [JsonProperty("episodes_count")] public ulong? EpisodesCount { get; init; }
    [JsonProperty("manga_time")] public DedicatedTime MangaTime { get; init; }
    [JsonProperty("anime_time")] public DedicatedTime AnimeTime { get; init; }
    [JsonProperty("readed_more_than_once")] public ulong? ReadMoreThanOnce { get; init; }
    [JsonProperty("wached_more_than_once")] public ulong? WatchedMoreThanOnce { get; init; }
    [JsonProperty("anime_mean_score")] public MeanScore AnimeMeanScore { get; init; }
    [JsonProperty("manga_mean_score")] public MeanScore MangaMeanScore { get; init; }
    [JsonProperty("anime_status")] public SeriesStatus AnimeStatus { get; init; }
    [JsonProperty("manga_status")] public SeriesStatus MangaStatus { get; init; }
}

public class ReadSpeed
{
    [JsonProperty("manga_read_time")] public ulong? MangaReadTime { get; init; }
    [JsonProperty("manga_proc")] public double? MangaProgress { get; init; }
    [JsonProperty("vn_proc")] public double? VisualNovelProgress { get; init; }
}

public class DedicatedTime
{
    [JsonProperty("days")] public ulong? Days { get; init; }
    [JsonProperty("years")] public ulong? Years { get; init; }
    [JsonProperty("hours")] public ulong? Hours { get; init; }
    [JsonProperty("months")] public ulong? Months { get; init; }
    [JsonProperty("minutes")] public ulong? Minutes { get; init; }
}

public class MeanScore
{
    [JsonProperty("count")] public ulong? Count { get; init; }
    [JsonProperty("rating")] public double? Rating { get; init; }
}

public class SeriesStatus
{
    [JsonProperty("in_progress")] public ulong? InProgress { get; init; }
    [JsonProperty("completed")] public ulong? Completed { get; init; }
    [JsonProperty("dropped")] public ulong? Dropped { get; init; }
    [JsonProperty("skipped")] public ulong? Skipped { get; init; }
    [JsonProperty("in_plan")] public ulong? InPlan { get; init; }
    [JsonProperty("on_hold")] public ulong? OnHold { get; init; }
}

public class FavouriteCharacter
{
    [JsonProperty("character_id")] public ulong Id { get; init; }
    [JsonProperty("first_name")] public string FirstName { get; init; }
    [JsonProperty("last_name")] public string LastName { get; init; }
    [JsonProperty("picture")] public ulong? PictureId { get; init; }
}

public class SeasonalAnimeInfo
{
    [JsonProperty("titles")] public List<ulong> TitleIds { get; init; }
    [JsonProperty("characters")] public List<ulong> CharacterIds { get; init; }
}

public class CharacterFlat
{
    [JsonProperty("c")] public string CharacterId { get; init; }
    [JsonProperty("p")] public string PictureId { get; init; }
    [JsonProperty("f")] public string FirstName { get; init; }
    [JsonProperty("l")] public string LastName { get; init; }
    [JsonProperty("t")] public string TitleName { get; init; }
    [JsonProperty("n")] public string TitleId { get; init; }
}

public class TitleCharacters
{
    [JsonProperty("relations")] public List<Relation> Relations { get; init; }
}

public class TitleReviews
{
    [JsonProperty("reviews")] public List<Review> Reviews { get; init; }
}

public class TitleRelations
{
    [JsonProperty("related_titles")] public List<TitleRelation> RelatedTitles { get; init; }
}

public class SearchResult
{
    [JsonProperty("per_page")] public long? PerPage { get; init; }
    [JsonProperty("offset")] public long? Offset { get; init; }
    [JsonProperty("total_items")] public long? TotalItems { get; init; }
    [JsonProperty("result_type")] public string ResultType { get; init; }
    [JsonProperty("items")] public List<SearchItem> Items { get; init; }
}

public class SearchItem
{
    [JsonProperty("title_id")] public string TitleId { get; init; }
    [JsonProperty("title")] public string Title { get; init; }
    [JsonProperty("type")] public string Type { get; init; }
    [JsonProperty("title_status")] public string TitleStatus { get; init; }
    [JsonProperty("cover_artifact_id")] public string CoverArtifactId { get; init; }
    [JsonProperty("rating_total")] public double? RatingTotal { get; init; }
    [JsonProperty("rating_story")] public double? RatingStory { get; init; }
    [JsonProperty("rating_music")] public double? RatingMusic { get; init; }
    [JsonProperty("rating_graphics")] public double? RatingGraphics { get; init; }
    [JsonProperty("rating_lines")] public double? RatingLines { get; init; }
    [JsonProperty("episodes")] public string Episodes { get; init; }
    [JsonProperty("chapters")] public long? Chapters { get; init; }
    [JsonProperty("volumes")] public long? Volumes { get; init; }
    [JsonProperty("genres")] public List<string> Genres { get; init; }
}

public class Status
{
    [JsonProperty("status")] public string ResponseStatus { get; init; }
}

public class IncreaseWatchedResult
{
    [JsonProperty("title_id")] public string TitleId { get; init; }
    [JsonProperty("status")] public string Status { get; init; }
    [JsonProperty("episode_added")] public string EpisodeAdded { get; init; }
}

public class TitleStatusAfterChange
{
    [JsonProperty("status")] public string Status { get; init; }
    [JsonProperty("series_status")] public TitleStatus TitleStatus { get; init; }
}

public class TitleStatus
{
    [JsonProperty("watched_status_id")] public string WatchedStatusId { get; init; }
    [JsonProperty("user_id")] public string UserId { get; init; }
    [JsonProperty("title_id")] public string TitleId { get; init; }
    [JsonProperty("watch_status")] public string WatchStatus { get; init; }
    [JsonProperty("is_favourite")] public string IsFavourite { get; init; }
}

public class Logging
{
    [JsonProperty("logged_user")] public LoggedUser LoggedUser { get; init; }
    [JsonProperty("hash")] public string Hash { get; init; }
    [JsonProperty("session")] public Session Session { get; init; }
}

public class LoggedUser
{
    [JsonProperty("user_id")] public string UserId { get; init; }
    [JsonProperty("name")] public string Name { get; init; }
    [JsonProperty("rank")] public string Rank { get; init; }
    [JsonProperty("avatar")] public string Avatar { get; init; }
    [JsonProperty("about_me")] public string AboutMe { get; init; }
}
