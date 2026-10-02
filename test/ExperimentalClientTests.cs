namespace ShindenAPI.Test;

[TestClass]
public class ExperimentalClientTests : TestBase
{
    [TestMethod]
    public async Task SeasonProjectionReturnsTitlesOrCharacters()
    {
        var titleIds = await _api.Experimental.AnimeFromSeasonAsync();
        var characterIds = await _api.Experimental.AnimeFromSeasonAsync(characters: true);

        titleIds.Get().Should().Equal(101UL, 102UL);
        characterIds.Get().Should().Equal(201UL, 202UL);
    }

    [TestMethod]
    public async Task BulkCharacterEndpointsRemainAvailable()
    {
        var allIds = await _api.Experimental.CharacterIdsAsync();
        var animeIds = await _api.Experimental.AnimeCharacterIdsAsync();
        var mangaIds = await _api.Experimental.MangaCharacterIdsAsync();
        var flat = await _api.Experimental.AllCharactersFlatAsync();

        allIds.Get().Should().Equal(1UL, 2UL);
        animeIds.Get().Should().Equal(3UL, 4UL);
        mangaIds.Get().Should().Equal(5UL);
        flat.Get().Should().ContainSingle(item => item.CharacterId == "1");
    }
}