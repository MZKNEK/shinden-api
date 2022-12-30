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
}
