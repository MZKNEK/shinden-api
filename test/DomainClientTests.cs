namespace ShindenAPI.Test;

[TestClass]
public class DomainClientTests : TestBase
{
    [TestMethod]
    public async Task CurrentSeasonAcceptsStringCharacterIds()
    {
        var season = (await _api.Episodes.CurrentSeasonAsync()).Get();
        season.TitleIds.Should().Equal(101UL, 102UL);
        season.CharacterIds.Should().Equal(201UL, 202UL);
    }

    [TestMethod]
    public async Task CurrentSeasonExposesSeasonMetadata()
    {
        var season = (await _api.Episodes.CurrentSeasonAsync()).Get();
        season.Season.Year.Should().Be(2026);
        season.Season.Season.Should().Be("fall");
        season.Season.Value.Should().Be(Queries.V1.Season.Fall);
        season.UpdatedAt.Should().Be(new DateTime(2026, 11, 7, 12, 0, 0));
        season.CharacterTitles.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { CharacterId = 201UL, TitleId = 101UL });
    }

    [DataTestMethod]
    [DataRow("winter", Queries.V1.Season.Winter)]
    [DataRow("Spring", Queries.V1.Season.Spring)]
    [DataRow("summer", Queries.V1.Season.Summer)]
    [DataRow("autumn", Queries.V1.Season.Fall)]
    [DataRow("fall", Queries.V1.Season.Fall)]
    [DataRow("monsoon", null)]
    [DataRow(null, null)]
    public void SeasonNamesMapToEnum(string? name, Queries.V1.Season? expected)
        => new Models.TitleByIdSeason { Season = name! }.Value.Should().Be(expected);

    [TestMethod]
    public void CurrentSeasonToleratesResponsesWithoutNewFields()
    {
        var season = Newtonsoft.Json.JsonConvert.DeserializeObject<Models.SeasonalAnimeInfo>("""{"titles":[1],"characters":[2]}""")!;
        season.Season.Should().BeNull();
        season.UpdatedAt.Should().BeNull();
        season.CharacterTitles.Should().BeEmpty();
    }

    [TestMethod]
    public async Task FlatListsExposeRankingInsteadOfTitleId()
    {
        var all = (await _api.Characters.FlatListAsync()).Get().Single();
        var anime = (await _api.Characters.AnimeFlatListAsync()).Get().Single();
        all.CharacterId.Should().Be(1);
        all.RankingPosition.Should().Be(120);
        anime.CharacterId.Should().Be(all.CharacterId);
    }
}
