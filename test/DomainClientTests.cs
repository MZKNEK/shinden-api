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
    public async Task FlatListsExposeRankingInsteadOfTitleId()
    {
        var all = (await _api.Characters.FlatListAsync()).Get().Single();
        var anime = (await _api.Characters.AnimeFlatListAsync()).Get().Single();
        all.CharacterId.Should().Be(1);
        all.RankingPosition.Should().Be(120);
        anime.CharacterId.Should().Be(all.CharacterId);
    }
}
