using static ShindenAPI.Queries.Search.TitleQuick;

namespace ShindenAPI.Test;

[TestClass]
public class ShindenApiTest : TestBase
{
    [TestMethod]
    public async Task SearchUserTest()
    {
        var result = await _api.AskAsync(new Queries.Search.User("sniku"));
        result.IsOk().Should().BeTrue();
        await Verify(result.Get());
    }

    [TestMethod]
    public async Task SearchStaffTest()
    {
        var result = await _api.AskAsync(new Queries.Search.Staff("Marina Inoue"));
        result.IsOk().Should().BeTrue();
        await Verify(result.Get());
    }

    [TestMethod]
    public async Task SearchCharacterTest()
    {
        var result = await _api.AskAsync(new Queries.Search.Character("Gintoki"));
        result.IsOk().Should().BeTrue();
        await Verify(result.Get());
    }

    [TestMethod]
    public async Task SearchAnimeQuickTest()
    {
        var result = await _api.AskAsync(new Queries.Search.TitleQuick("Gintama", SearchType.Anime));
        result.IsOk().Should().BeTrue();
        await Verify(result.Get());
    }

    [TestMethod]
    public async Task SearchMangaQuickTest()
    {
        var result = await _api.AskAsync(new Queries.Search.TitleQuick("Dorohedoro", SearchType.Manga));
        result.IsOk().Should().BeTrue();
        await Verify(result.Get());
    }

    [TestMethod]
    public async Task SearchBothQuickTest()
    {
        var result = await _api.AskAsync(new Queries.Search.TitleQuick("Bleach", SearchType.Both));
        result.IsOk().Should().BeTrue();
        await Verify(result.Get());
    }
}
