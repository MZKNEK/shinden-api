using Newtonsoft.Json;
using ShindenAPI.Models;

namespace ShindenAPI.Test;

[TestClass]
public class DtoParsingTests
{
    [TestMethod]
    public void EpisodeAndHistoryModelsDeserializeSnakeCasePayloads()
    {
        var episode = JsonConvert.DeserializeObject<Episode>("""
        {
            "id": 12,
            "anime_id": 34,
            "is_filler": true,
            "has_online": true,
            "online_id": 56,
            "type": "standard",
            "air_date": "2026-09-01",
            "air_channel": "TV",
            "episode_number": 7,
            "episode_length": "24 min",
            "episode_title": { "title": "Test episode", "language": "en" }
        }
        """)!;

        var newEpisode = JsonConvert.DeserializeObject<NewEpisode>("""
        {
            "episode_id": 91,
            "anime_id": 34,
            "cover_id": 101,
            "add_date": "2026-09-02",
            "anime_title": "Test anime",
            "episode_number": 8,
            "episode_length": "24 min",
            "subtitles_language": "en"
        }
        """)!;

        var watched = JsonConvert.DeserializeObject<LastWatched>("""
        {
            "view_cnt": 2,
            "is_filler": false,
            "title_id": 34,
            "is_special": false,
            "episode_no": 8,
            "type": "tv",
            "episode_id": 91,
            "episodes_cnt": 12,
            "anime_title": "Test anime",
            "title_cover_id": 101,
            "episode_title": "Test episode",
            "watched_date": "2026-09-02"
        }
        """)!;

        episode.Id.Should().Be(12);
        episode.AnimeId.Should().Be(34);
        episode.EpisodeTitle.Title.Should().Be("Test episode");
        newEpisode.Id.Should().Be(91);
        newEpisode.CoverId.Should().Be(101);
        watched.EpisodeNumber.Should().Be(8);
        watched.CoverId.Should().Be(101);
    }

    [TestMethod]
    public void RecommendationAndUserModelsDeserializeNestedPayloads()
    {
        var recommendation = JsonConvert.DeserializeObject<Recommendation>("""
        {
            "id": 5,
            "rating": 9,
            "rate_count": 3,
            "content": "Recommended",
            "add_date": "2026-09-03",
            "base_title_id": 34,
            "author": { "user_id": 85, "name": "Tester", "avatar": "18" },
            "recommended_title": { "title_id": 35, "title": "Related", "cover": "102" }
        }
        """)!;

        var user = JsonConvert.DeserializeObject<UserInfo>("""
        {
            "user_id": 85,
            "name": "Tester",
            "rank": "User",
            "list_stats": {
                "read_speed": { "manga_read_time": 10, "manga_proc": 1.5, "vn_proc": 2.5 },
                "chapters_count": 20,
                "episodes_count": 30,
                "anime_time": { "days": 1, "hours": 2 },
                "manga_time": { "minutes": 40 },
                "anime_mean_score": { "count": 3, "rating": 8.5 },
                "manga_mean_score": { "count": 2, "rating": 7.5 },
                "anime_status": { "completed": 4 },
                "manga_status": { "in_progress": 2 }
            }
        }
        """)!;

        recommendation.Author.Id.Should().Be(85);
        recommendation.RecommendedTitle.Id.Should().Be(35);
        user.Id.Should().Be(85);
        user.ListStats.ReadSpeed.MangaProgress.Should().Be(1.5);
        user.ListStats.AnimeTime.Days.Should().Be(1);
        user.ListStats.AnimeMeanScore.Rating.Should().Be(8.5);
        user.ListStats.MangaStatus.InProgress.Should().Be(2);
    }
}
