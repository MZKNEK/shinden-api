namespace ShindenAPI.Test;

[TestClass]
public class ShindenApiTest : TestBase
{
    [TestMethod]
    public async Task SearchUserTest()
    {
        var result = await _api.AskAsync(new Queries.Search.User("sniku"));
        result.IsOk().Should().BeTrue();
        await Verify(result.Value);
    }

    [TestMethod]
    public async Task SearchStaffTest()
    {
        var result = await _api.AskAsync(new Queries.Search.Staff("Marina Inoue"));
        result.IsOk().Should().BeTrue();
        await Verify(result.Value);
    }

    [TestMethod]
    public async Task SearchCharacterTest()
    {
        var result = await _api.AskAsync(new Queries.Search.Character("Gintoki"));
        result.IsOk().Should().BeTrue();
        await Verify(result.Value);
    }
}
