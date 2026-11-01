// Response models for the Shinden v1 API.
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ShindenAPI.Models;

public class TitleByIdSeason
{
    [JsonProperty("year")] public long Year { get; init; }
    [JsonProperty("season")] public string Season { get; init; }
}

public class TitleById
{
    [JsonProperty("id", Required = Required.Always)] public ulong Id { get; init; }
    [JsonProperty("title")] public string Title { get; init; }
    [JsonProperty("cover")] public string Cover { get; init; }
    [JsonProperty("type")] public string Type { get; init; }
    [JsonProperty("episodes")] public long? Episodes { get; init; }
    [JsonProperty("season")] public TitleByIdSeason Season { get; init; }
}

public class SearchTag
{
    [JsonProperty("tag_id")] public ulong TagId { get; init; }
    [JsonProperty("tag_type")] public string TagType { get; init; }
    [JsonProperty("tag_name")] public string TagName { get; init; }
    [JsonProperty("parent_id")] public ulong? ParentId { get; init; }
    [JsonProperty("is_accepted")] public bool IsAccepted { get; init; }
    [JsonProperty("search")] public string Search { get; init; }
    [JsonProperty("description")] public string Description { get; init; }
    [JsonProperty("tt")] public string Tt { get; init; }
}

public class SearchResult
{
    [JsonProperty("total")] public long Total { get; init; }
    [JsonProperty("items")] public List<SearchItem> Items { get; init; }
}

public class SearchItem
{
    [JsonProperty("title_id")] public ulong TitleId { get; init; }
    [JsonProperty("title")] public string Title { get; init; }
    [JsonProperty("obj")] public string Obj { get; init; }
    [JsonProperty("type")] public string Type { get; init; }
    [JsonProperty("kind")] public string Kind { get; init; }
    [JsonProperty("title_status")] public string TitleStatus { get; init; }
    [JsonProperty("cover")] public ulong? Cover { get; init; }
    [JsonProperty("cover_hidden")] public string CoverHidden { get; init; }
    [JsonProperty("premiere_date")] public string PremiereDate { get; init; }
    [JsonProperty("finish_date")] public string FinishDate { get; init; }
    [JsonProperty("mpaa_rating")] public string MpaaRating { get; init; }
    [JsonProperty("dmca")] public string Dmca { get; init; }
    [JsonProperty("episodes")] public long? Episodes { get; init; }
    [JsonProperty("episode_time")] public long? EpisodeTime { get; init; }
    [JsonProperty("volumes")] public long? Volumes { get; init; }
    [JsonProperty("chapters")] public long? Chapters { get; init; }
    [JsonProperty("ranking_position")] public long? RankingPosition { get; init; }
    [JsonProperty("ranking_rate")] public double? RankingRate { get; init; }
    [JsonProperty("members")] public long Members { get; init; }
    [JsonProperty("rating_total")] public double? RatingTotal { get; init; }
    [JsonProperty("rating_story")] public double? RatingStory { get; init; }
    [JsonProperty("rating_titlecahracters")] public double? RatingCharacters { get; init; }
    [JsonProperty("rating_graphics")] public double? RatingGraphics { get; init; }
    [JsonProperty("rating_music")] public double? RatingMusic { get; init; }
    [JsonProperty("rating_lines")] public double? RatingLines { get; init; }
    [JsonProperty("tags")] public List<long> Tags { get; init; }
    [JsonProperty("genres")] public List<long> Genres { get; init; }
    [JsonProperty("title_url")] public string TitleUrl { get; init; }
    [JsonProperty("other_to")] public string OtherTo { get; init; }
    [JsonProperty("next_episode")] public long? NextEpisode { get; init; }
    [JsonProperty("next_airing_at")] public DateTimeOffset? NextAiringAt { get; init; }
    [JsonProperty("favourite_cnt")] public long FavouriteCount { get; init; }
}

public class CatalogAnimeCover
{
    [JsonProperty("large")] public string Large { get; init; }
    [JsonProperty("full")] public string Full { get; init; }
}

public class CatalogAnimeRatings
{
    [JsonProperty("total")] public double? Total { get; init; }
    [JsonProperty("story")] public double? Story { get; init; }
    [JsonProperty("graphics")] public double? Graphics { get; init; }
    [JsonProperty("music")] public double? Music { get; init; }
    [JsonProperty("characters")] public double? Characters { get; init; }
}

public class CatalogAnime
{
    [JsonProperty("id", Required = Required.Always)] public ulong Id { get; init; }
    [JsonProperty("title")] public string Title { get; init; }
    [JsonProperty("type")] public string Type { get; init; }
    [JsonProperty("year")] public long Year { get; init; }
    [JsonProperty("url")] public string Url { get; init; }
    [JsonProperty("cover")] public CatalogAnimeCover Cover { get; init; }
    [JsonProperty("description")] public string Description { get; init; }
    [JsonProperty("ratings")] public CatalogAnimeRatings Ratings { get; init; }
}

public class CatalogCharacterImage
{
    [JsonProperty("large")] public string Large { get; init; }
    [JsonProperty("full")] public string Full { get; init; }
}

public class CatalogCharacterFacts
{
    [JsonProperty("gender")] public string Gender { get; init; }
    [JsonProperty("age")] public string Age { get; init; }
    [JsonProperty("birthDay")] public string BirthDay { get; init; }
    [JsonProperty("deathDay")] public string DeathDay { get; init; }
    [JsonProperty("height")] public long Height { get; init; }
    [JsonProperty("weight")] public long Weight { get; init; }
    [JsonProperty("bloodtype")] public string Bloodtype { get; init; }
    [JsonProperty("bust")] public long Bust { get; init; }
    [JsonProperty("waist")] public long Waist { get; init; }
    [JsonProperty("hips")] public long Hips { get; init; }
    [JsonProperty("birth")] public PartialDate Birth { get; init; }
    [JsonProperty("death")] public PartialDate Death { get; init; }
}

public class CatalogCharacter
{
    [JsonProperty("id", Required = Required.Always)] public ulong Id { get; init; }
    [JsonProperty("firstName")] public string FirstName { get; init; }
    [JsonProperty("lastName")] public string LastName { get; init; }
    [JsonProperty("url")] public string Url { get; init; }
    [JsonProperty("image")] public CatalogCharacterImage Image { get; init; }
    [JsonProperty("biography")] public string Biography { get; init; }
    [JsonProperty("facts")] public CatalogCharacterFacts Facts { get; init; }
}

public class PartialDate
{
    [JsonProperty("day")] public long? Day { get; init; }
    [JsonProperty("month")] public long? Month { get; init; }
    [JsonProperty("year")] public long? Year { get; init; }
}

public class StatusCounts
{
    [JsonProperty("inProgress")] public long InProgress { get; init; }
    [JsonProperty("completed")] public long Completed { get; init; }
    [JsonProperty("skip")] public long Skip { get; init; }
    [JsonProperty("hold")] public long Hold { get; init; }
    [JsonProperty("dropped")] public long Dropped { get; init; }
    [JsonProperty("plan")] public long Plan { get; init; }
}

public class UserStatsAnime
{
    [JsonProperty("statusCounts")] public StatusCounts StatusCounts { get; init; }
    [JsonProperty("total")] public long Total { get; init; }
    [JsonProperty("episodesWatched")] public long EpisodesWatched { get; init; }
    [JsonProperty("episodesRewatched")] public long EpisodesRewatched { get; init; }
    [JsonProperty("watchMinutes")] public long WatchMinutes { get; init; }
    [JsonProperty("meanScore")] public double? MeanScore { get; init; }
    [JsonProperty("scoresCount")] public long ScoresCount { get; init; }
}

public class UserStatsManga
{
    [JsonProperty("statusCounts")] public StatusCounts StatusCounts { get; init; }
    [JsonProperty("total")] public long Total { get; init; }
    [JsonProperty("novelTotal")] public long? NovelTotal { get; init; }
    [JsonProperty("chaptersRead")] public long ChaptersRead { get; init; }
    [JsonProperty("chaptersReread")] public long ChaptersReread { get; init; }
    [JsonProperty("readMinutes")] public long ReadMinutes { get; init; }
    [JsonProperty("meanScore")] public double? MeanScore { get; init; }
    [JsonProperty("scoresCount")] public long ScoresCount { get; init; }
}

public class UserStatsReadSpeed
{
    [JsonProperty("mangaMinutesPerChapter")] public double? MangaMinutesPerChapter { get; init; }
    [JsonProperty("mangaPercentVsDefault")] public double MangaPercentVsDefault { get; init; }
    [JsonProperty("vnMinutesPerChapter")] public double? VnMinutesPerChapter { get; init; }
    [JsonProperty("vnPercentVsDefault")] public double VnPercentVsDefault { get; init; }
}

public class UserStats
{
    [JsonProperty("userId", Required = Required.Always)] public ulong UserId { get; init; }
    [JsonProperty("anime")] public UserStatsAnime Anime { get; init; }
    [JsonProperty("manga")] public UserStatsManga Manga { get; init; }
    [JsonProperty("readSpeed")] public UserStatsReadSpeed ReadSpeed { get; init; }
    [JsonProperty("generatedAt")] public DateTimeOffset GeneratedAt { get; init; }
}

public class IdName
{
    [JsonProperty("id")] public ulong Id { get; init; }
    [JsonProperty("name")] public string Name { get; init; }
}

public class PwTag
{
    [JsonProperty("id")] public ulong Id { get; init; }
    [JsonProperty("name")] public string Name { get; init; }
    [JsonProperty("system")] public bool System { get; init; }
    [JsonProperty("count")] public long Count { get; init; }
}

public class PwCardsPage
{
    [JsonProperty("status")] public string Status { get; init; }
    [JsonProperty("version")] public long Version { get; init; }
    [JsonProperty("items")] public List<PwCard> Items { get; init; }
    [JsonProperty("nextCursor")] public string NextCursor { get; init; }
}

public class PwCardBonus
{
    [JsonProperty("attack")] public long Attack { get; init; }
    [JsonProperty("defence")] public long Defence { get; init; }
    [JsonProperty("health")] public long Health { get; init; }
}

public class PwCard
{
    [JsonProperty("id")] public ulong Id { get; init; }
    [JsonProperty("name")] public string Name { get; init; }
    [JsonProperty("characterId")] public long CharacterId { get; init; }
    [JsonProperty("title")] public string Title { get; init; }
    [JsonProperty("rarity")] public string Rarity { get; init; }
    [JsonProperty("ultimate")] public bool Ultimate { get; init; }
    [JsonProperty("quality")] public long Quality { get; init; }
    [JsonProperty("dere")] public string Dere { get; init; }
    [JsonProperty("power")] public double Power { get; init; }
    [JsonProperty("scalpel")] public bool Scalpel { get; init; }
    [JsonProperty("cage")] public bool Cage { get; init; }
    [JsonProperty("tradable")] public bool Tradable { get; init; }
    [JsonProperty("expedition")] public long Expedition { get; init; }
    [JsonProperty("cursed")] public bool Cursed { get; init; }
    [JsonProperty("image")] public string Image { get; init; }
    [JsonProperty("health")] public long Health { get; init; }
    [JsonProperty("attack")] public long Attack { get; init; }
    [JsonProperty("defence")] public long Defence { get; init; }
    [JsonProperty("affection")] public double Affection { get; init; }
    [JsonProperty("market")] public double Market { get; init; }
    [JsonProperty("restarts")] public long Restarts { get; init; }
    [JsonProperty("wanted")] public long Wanted { get; init; }
    [JsonProperty("created")] public string Created { get; init; }
    [JsonProperty("exp")] public double Exp { get; init; }
    [JsonProperty("upgrades")] public long Upgrades { get; init; }
    [JsonProperty("unique")] public bool Unique { get; init; }
    [JsonProperty("figure")] public bool Figure { get; init; }
    [JsonProperty("bonus")] public PwCardBonus Bonus { get; init; }
    [JsonProperty("fatigue")] public double Fatigue { get; init; }
    [JsonProperty("akc")] public long Akc { get; init; }
    [JsonProperty("scalpelDate")] public string ScalpelDate { get; init; }
    [JsonProperty("border")] public bool Border { get; init; }
    [JsonProperty("source")] public string Source { get; init; }
    [JsonProperty("tags")] public List<long> Tags { get; init; }
    [JsonProperty("active")] public bool Active { get; init; }
    [JsonProperty("expeditionEnd")] public string ExpeditionEnd { get; init; }
    [JsonProperty("borderOverflow")] public long BorderOverflow { get; init; }
    [JsonProperty("curse")] public long Curse { get; init; }
}
