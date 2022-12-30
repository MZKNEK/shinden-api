namespace ShindenAPI.Test;

[TestClass]
public class ShindenApiTest : TestBase
{
    [TestMethod]
    public async Task SearchUserTest()
    {
        var api = new Shinden(new(_uri), new()
            {
                Token = _token,
                Marmolade = _secret,
                UserAgent = "PTestShinden",
            });

        var result = await api.AskAsync(new Queries.Search.User("sniku"));
        result.IsOk().Should().BeTrue();
        await Verify(result.Value);
    }
}
