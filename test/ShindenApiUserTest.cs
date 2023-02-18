namespace ShindenAPI.Test;

[TestClass]
public class ShindenApiUserTest : TestBaseUserCred
{
    [TestMethod]
    public async Task LoginAsUserTest()
    {
        var result = await _api.AskAsync(new Queries.Search.User("tsanapi"));
        result.IsOk().Should().BeTrue();
        _api.IsValidUserSession().Should().BeTrue();
    }

    [TestMethod]
    public async Task AddToFavsTest()
    {
        var user = await _api.LoginAsUserAsync();
        var resultTile = await _api.AskAsync(new Queries.User.FavouriteAdd(x =>
            x.WithTitleId(1).WithUserId(user.Id).Build()));
        resultTile.IsOk().Should().BeTrue();

        var resultStaff = await _api.AskAsync(new Queries.User.FavouriteAdd(x =>
            x.WithStaffId(1).WithUserId(user.Id).Build()));
        resultStaff.IsOk().Should().BeTrue();

        var resultCharacter = await _api.AskAsync(new Queries.User.FavouriteAdd(x =>
            x.WithCharacterId(1).WithUserId(user.Id).Build()));
        resultCharacter.IsOk().Should().BeTrue();

        await Verify((resultTile.Get(), resultStaff.Get(), resultCharacter.Get()));
    }

    [TestMethod]
    public async Task RemoveFromFavsTest()
    {
        var user = await _api.LoginAsUserAsync();
        var result = await _api.AskAsync(new Queries.User.FavouriteRemove(x =>
            x.WithTitleId(1).WithUserId(user.Id).Build()));

        result.IsOk().Should().BeTrue();
        await Verify(result.Get());
    }
}
