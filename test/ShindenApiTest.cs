using ShindenAPI.Queries.Search;

namespace ShindenAPI.Test;

[TestClass]
public class ShindenApiTest : TestBase
{
    [TestMethod]
    public async Task SearchUsersReadsBooleanAvatar()
    {
        var user = (await _api.Search.UsersAsync("sniku")).Get().Single();
        user.Id.Should().Be(85);
        user.HasAvatar.Should().BeTrue();
        user.AvatarUrl.Should().EndWith("/85.jpg?v1");
    }

    [TestMethod]
    public async Task SearchPeopleReadsTypedPicturesAndExactMatch()
    {
        var staff = (await _api.Search.StaffAsync("Marina Inoue")).Get().Single();
        var character = (await _api.Search.CharactersAsync("Gintoki")).Get().Single();
        staff.Id.Should().Be(156);
        staff.PictureId.Should().Be(176530);
        character.Id.Should().Be(663);
        character.IsExactMatch.Should().BeTrue();
        character.PictureUrl.Should().EndWith("/274727.jpg");
    }

    [DataTestMethod]
    [DataRow("Gintama", TitleQuick.SearchType.Anime, 1178UL)]
    [DataRow("Dorohedoro", TitleQuick.SearchType.Manga, 2491UL)]
    [DataRow("Bleach", TitleQuick.SearchType.Both, 3UL)]
    public async Task SearchTitlesReadsNumbersAndServerUrl(string query, TitleQuick.SearchType type, ulong id)
    {
        var title = (await _api.Search.TitlesAsync(query, type)).Get().Single();
        title.Id.Should().Be(id);
        title.CoverId.Should().NotBeNull();
        title.TitleUrl.Should().Be($"https://shinden.pl/titles/{id}-example");
    }
}
