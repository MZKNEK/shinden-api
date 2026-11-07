using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ShindenAPI.Models;

namespace ShindenAPI.Test;

[TestClass]
public class DtoParsingTests
{
    [DataTestMethod]
    [DataRow("0", false)]
    [DataRow("1", true)]
    [DataRow("true", true)]
    [DataRow("false", false)]
    public void PhpReviewsMapFlatFieldsAndStringFlags(string flag, bool expected)
    {
        var row = JObject.Parse("""
        {"review_id":"7","rating":"8","rate_count":"3","is_abstract":"1","review":"Review text",
         "edited_at":null,"name":"Author","user_id":"42","avatar":"18","enter_cnt":"12","read_cnt":"6"}
        """);
        row["is_abstract"] = flag;
        var review = row.ToObject<Review>()!;
        review.Id.Should().Be(7);
        review.Rating.Should().Be(8);
        review.RateCount.Should().Be(3);
        review.IsAbstract.Should().Be(expected);
        review.Content.Should().Be("Review text");
        review.EnterCount.Should().Be(12);
        review.ReadCount.Should().Be(6);
        review.Author.Id.Should().Be(42);
        review.Author.Name.Should().Be("Author");
        review.Author.Avatar.Should().Be("18");
        review.AdditionalData.Should().ContainKey("edited_at");
        JsonConvert.SerializeObject(review).Should().Contain("Review text");
    }

    [DataTestMethod]
    [DataRow("true", true)]
    [DataRow("false", false)]
    [DataRow("0", false)]
    [DataRow("1", true)]
    public void PhpReviewsAlsoAcceptTypedFlags(string flag, bool expected)
    {
        var review = JsonConvert.DeserializeObject<Review>("{\"id\":7,\"is_abstract\":" + flag + "}")!;
        review.IsAbstract.Should().Be(expected);
    }

    [TestMethod]
    public void PhpRecommendationsMapFlatAuthorAndRecommendedTitle()
    {
        var recommendation = JsonConvert.DeserializeObject<Recommendation>("""
        {"recommendation_id":"9","rec_title_id":"11","rtitle_id":"12","title_id":"11",
         "cover_artifact_id":"13","title":"Recommended title","type":"Anime","avatar":"18",
         "rating":"8","rate_count":"3","name":"Author","user_id":"42",
         "add_date":"2026-11-01 10:00:00","recommendation":"Recommendation text"}
        """)!;
        recommendation.Id.Should().Be(9);
        recommendation.Content.Should().Be("Recommendation text");
        recommendation.BaseTitleId.Should().Be(11);
        recommendation.Author.Id.Should().Be(42);
        recommendation.RecommendedTitle.Id.Should().Be(12);
        recommendation.RecommendedTitle.Title.Should().Be("Recommended title");
        recommendation.RecommendedTitle.CoverId.Should().Be("13");
    }

    [DataTestMethod]
    [DataRow("""[{"id":11,"title":"Title","cover":null,"episodes":12,"season":{"year":2026,"season":"fall"}}]""")]
    [DataRow("""{"items":[{"id":11,"title":"Title","cover":null,"episodes":12,"season":{"year":2026,"season":"fall"}}]}""")]
    public async Task TitlesByIdsAcceptsArrayAndItemsWrapper(string response)
    {
        using var handler = new RecordingHandler { Response = response };
        using var api = new Shinden(new Uri("https://fake.shinden.test/"), new Credentials.Auth { UserAgent = "test" }, handler: handler);
        var title = (await api.Search.TitlesByIdsAsync(new[] { 11UL })).Get().Single();
        title.Id.Should().Be(11);
        title.Cover.Should().BeNull();
        title.Episodes.Should().Be(12);
        title.Season.Year.Should().Be(2026);
    }

    [TestMethod]
    public async Task PhpReviewQuerySuppliesTitleIdFromRequest()
    {
        using var handler = new RecordingHandler { Response = """{"reviews":[{"review_id":"7","is_abstract":"1","review":"Review text","user_id":"42","name":"Author"}]}""" };
        using var api = new Shinden(config => config.WithUri("https://fake.shinden.test/")
            .WithAuth(new Credentials.Auth { UserAgent = "test" })
            .WithLegacyAuth(new Credentials.Auth { Token = "old-key", UserAgent = "test" }).Build(), handler: handler);
        var review = (await api.Titles.ReviewsAsync(11)).Get().Reviews.Single();
        review.TitleId.Should().Be(11);
        review.Id.Should().Be(7);
        review.IsAbstract.Should().BeTrue();
    }

    [TestMethod]
    public void CompatibilityProfileReadsTopLevelStatsAndNullableScores()
    {
        var user = JsonConvert.DeserializeObject<UserInfo>("""
        {
            "user_id":"12345","vb_id":"0","name":"PrzykladowyNick","avatar":"0",
            "watched_status":{"in progress":"3","completed":"40","plan":"10","_total":53},
            "watched_cnt":"812","watch_time":{"y":0,"n":7,"d":7,"h":4,"m":12,"t":"10332"},
            "mean_anime_score":{"mean_score":"7.80","scores_cnt":"35"},
            "readed_status":{"completed":"5","_total":5},
            "mean_manga_score":{"mean_score":null,"scores_cnt":"0"},
            "chapters_cnt":"120","read_speed":{"manga_proc":100,"vn_proc":100}
        }
        """)!;
        user.Id.Should().Be(12345);
        user.Email.Should().BeNull();
        user.WatchedStatus["in progress"].Should().Be(3);
        user.WatchedStatus["_total"].Should().Be(53);
        user.WatchedCount.Should().Be(812);
        user.WatchTime.TotalMinutes.Should().Be(10332);
        user.MeanAnimeScore.Rating.Should().Be(7.8);
        user.MeanMangaScore.Rating.Should().BeNull();
        user.ReadSpeed.MangaProgress.Should().Be(100);
    }

    [TestMethod]
    public void CompatibilityCharacterReadsActualFieldNames()
    {
        var character = JsonConvert.DeserializeObject<CharacterInfo>("""
        {"character_id":"1000","first_name":"Imię","last_name":null,"is_real":"0",
         "birth_day":null,"age":"17","picture_artifact_id":"250000",
         "bio":{"character_biography_id":"7","biography":"Opis","lang":"pl"},
         "fav_stats":{"fav_cnt":"12"},"points":[{"user_id":"42","value":"2"}],
         "relations":[{"title_id":"11"}],"pictures":[{"artifact_id":"250001"}]}
        """)!;
        character.Id.Should().Be(1000);
        character.IsReal.Should().Be("0");
        character.PictureId.Should().Be(250000);
        character.Biography.Text.Should().Be("Opis");
        character.FavouriteStats.AdditionalData["fav_cnt"].Value<string>().Should().Be("12");
        character.Relations.Single().AdditionalData["title_id"].Value<string>().Should().Be("11");
    }

    [TestMethod]
    public void CompatibilityTitleAllowsEmptyTagArraysAndNumericTimestamps()
    {
        var title = JsonConvert.DeserializeObject<AnimeMangaInfo>("""
        {"title":{"title_id":"11","title":"Przykładowe anime","premiere_date":1033603200,
          "cover_artifact_id":"300000","anime":{"title_id":"11","episodes":"220","episode_time":"23","anime_type":"TV"},
          "tags":[],"description":{"description_id":"5","title_id":"11","lang_code":"pl","description":"Opis."},
          "title_other":[],"premiere_string":"03.10.2002","finish_string":"08.02.2007"}}
        """)!.Title;
        title.Id.Should().Be(11);
        title.StartDate.Should().Be("1033603200");
        title.Anime.EpisodesCount.Should().Be(220);
        title.Tags.Type.Should().Be(JTokenType.Array);
        title.Description.Text.Should().Be("Opis.");
        title.PremiereString.Should().Be("03.10.2002");
    }

    [TestMethod]
    public void HistoryRetainsUndocumentedPhpColumns()
    {
        var row = JsonConvert.DeserializeObject<LastWatched>("""
        {"watched_episode_id":"900001","user_id":"12345","episode_id":"55501","title_id":"11",
         "view_cnt":"1","created_time":"1761900000","episode_no":"5","title":"Przykładowe anime",
         "type":"Anime","episodes":"12","is_filler":"0","optional_column":null}
        """)!;
        row.EpisodeId.Should().Be(55501);
        row.Title.Should().Be("Przykładowe anime");
        row.EpisodesCount.Should().Be(12);
        row.AdditionalData["is_filler"].Value<string>().Should().Be("0");
        row.AdditionalData["optional_column"].Type.Should().Be(JTokenType.Null);
    }

    [TestMethod]
    public void ReadHistoryAcceptsFractionalChapterNumbers()
    {
        var rows = JsonConvert.DeserializeObject<List<LastRead>>("""
        [{"chapter_id":"822368","title_id":"60316","view_cnt":"1","chapter_no":"23","volume_no":"6"},
         {"chapter_id":"822308","title_id":"70144","view_cnt":"1","chapter_no":"19.1","volume_no":null},
         {"chapter_id":"341986","title_id":"1971","view_cnt":"1","chapter_no":"64.5","volume_no":"12"}]
        """)!;
        rows.Select(r => r.ChapterNumber).Should().Equal(23m, 19.1m, 64.5m);
        rows[1].VolumeNumber.Should().BeNull();
    }

    [TestMethod]
    public void V1SearchPreservesHiddenCoversAndServerUrls()
    {
        var title = JsonConvert.DeserializeObject<TitleSearchResult>("""
        {"title_id":11,"title":"A &amp; B","type":"Anime","kind":"TV","cover":null,
         "premiere_date":null,"other_to":"<em>A</em> B","title_url":"https://shinden.pl/titles/11-title"}
        """)!;
        title.Id.Should().Be(11);
        title.CoverId.Should().BeNull();
        title.Name.Should().Be("A &amp; B");
        title.TitleUrl.Should().Be("https://shinden.pl/titles/11-title");
        title.PictureUrl.Should().Contain("placeholders");
        var result = JsonConvert.DeserializeObject<SearchResult>("""
        {"total":1,"items":[{"title_id":11,"cover":null,"cover_hidden":"dmca","episodes":null,
         "ranking_rate":8.5,"rating_titlecahracters":9.2,"tags":[1,2],"genres":[3],"favourite_cnt":12}]}
        """)!;
        result.Total.Should().Be(1);
        var item = result.Items.Single();
        item.Cover.Should().BeNull();
        item.RatingCharacters.Should().Be(9.2);
        item.Tags.Should().Equal(1, 2);
        item.FavouriteCount.Should().Be(12);
    }

    [TestMethod]
    public void V1StatsAndCatalogKeepTypedNestedValues()
    {
        var stats = JsonConvert.DeserializeObject<UserStats>("""
        {"userId":12345,"anime":{"statusCounts":{"inProgress":3,"completed":40},"total":56,
         "episodesWatched":812,"watchMinutes":18676,"meanScore":7.8,"scoresCount":35},
         "manga":{"novelTotal":0,"chaptersRead":120,"meanScore":null},
         "readSpeed":{"mangaMinutesPerChapter":null,"mangaPercentVsDefault":100},"generatedAt":"2026-11-01T10:00:00Z"}
        """)!;
        stats.UserId.Should().Be(12345);
        stats.Anime.StatusCounts.InProgress.Should().Be(3);
        stats.Anime.MeanScore.Should().Be(7.8);
        stats.Manga.MeanScore.Should().BeNull();
        stats.ReadSpeed.MangaMinutesPerChapter.Should().BeNull();
        var character = JsonConvert.DeserializeObject<CatalogCharacter>("""
        {"id":1000,"firstName":"Imię","image":{"large":"https://example.test/large"},
         "facts":{"height":170,"birth":{"day":1,"month":null,"year":null}}}
        """)!;
        character.Id.Should().Be(1000);
        character.Facts.Birth.Day.Should().Be(1);
        character.Facts.Birth.Year.Should().BeNull();
    }

    [TestMethod]
    public void PocketWaifuParsesCursorCardFlagsAndEffects()
    {
        var page = JsonConvert.DeserializeObject<PwCardsPage>("""
        {"status":"ok","version":1761900000,"items":[{"id":42,"characterId":1000,"ultimate":true,
         "quality":5,"expedition":2,"power":12.5,"tags":null,"bonus":{"attack":10,"defence":20,"health":30}}],"nextCursor":"a+b/="}
        """)!;
        page.NextCursor.Should().Be("a+b/=");
        var card = page.Items.Single();
        card.Id.Should().Be(42);
        card.Ultimate.Should().BeTrue();
        card.Expedition.Should().Be(2);
        card.Bonus.Health.Should().Be(30);
        card.Tags.Should().BeNull();
        var details = JsonConvert.DeserializeObject<PwCardDetails>("""
        {"status":"ok","owner":null,"efekty":{"glowna":"effect","sss":"rare"},"tags":[]}
        """)!;
        details.Owner.Should().BeNull();
        details.Effects.Main.Should().Be("effect");
        details.Effects.SSS.Should().Be("rare");
    }

    [DataTestMethod]
    [DataRow("""{"lastonline":[{"episode_id":"91","title_id":"34","custom":"value"}]}""")]
    [DataRow("""{"lastonline":{"0":{"episode_id":"91","title_id":"34","custom":"value"}}}""")]
    public async Task NewEpisodeWrapperSupportsPhpArrayAndObject(string response)
    {
        using var handler = new RecordingHandler { Response = response };
        using var api = new Shinden(new Uri("https://fake.shinden.test/"),
            new Credentials.Auth { UserAgent = "test" }, handler: handler);
        var episode = (await api.Episodes.NewAsync()).Get().Single();
        episode.Id.Should().Be(91);
        episode.AdditionalData["custom"].Value<string>().Should().Be("value");
    }
}
