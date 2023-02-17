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
}
