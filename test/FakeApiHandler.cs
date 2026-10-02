using System.Net;
using System.Net.Http;
using System.Text;

namespace ShindenAPI.Test;

internal sealed class FakeApiHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath.Trim('/');
        var response = path switch
        {
            "api/user/login" => Json("{\"logged_user\":{\"user_id\":85,\"name\":\"tsanapi\",\"rank\":\"Tester\",\"avatar\":\"18\"},\"session\":{\"name\":\"tsanapi\",\"id\":\"session-id\"},\"hash\":\"session-hash\"}"),
            "api/user/search" => SearchUser(request),
            "api/staff/search" => Json("[{\"staff_id\":156,\"first_name\":\"Marina\",\"last_name\":\"Inoue\",\"all_names\":\"Marina Inoue\",\"picture\":\"176530\"}]"),
            "api/character/search" => Json("[{\"character_id\":663,\"first_name\":\"Gintoki\",\"last_name\":\"Sakata\",\"all_names\":\"Gintoki Sakata\",\"picture\":\"274727\"}]"),
            "api/character/connected" => Json("[1,2]"),
            "api/character/in-anime" => Json("[3,4]"),
            "api/character/in-manga" => Json("[5]"),
            "api/character/flat-list" => CharacterFlatList(),
            "api/character/flat-list-anime" => CharacterFlatList(),
            "api/character/flat-list-book" => CharacterFlatList(),
            "api/anime/current-season" => Json("{\"titles\":[101,102],\"characters\":[201,202]}"),
            "api/title/search" => SearchTitle(request),
            "api/userlist/85/fav" => Json("{\"updated\":1}"),
            _ => NotFound(path)
        };

        return Task.FromResult(response);
    }

    private static HttpResponseMessage SearchUser(HttpRequestMessage request)
    {
        var content = request.RequestUri!.Query.Contains("tsanapi", StringComparison.OrdinalIgnoreCase)
            ? "[{\"user_id\":85,\"name\":\"tsanapi\",\"rank\":\"Tester\",\"avatar\":\"18\"}]"
            : "[{\"user_id\":85,\"name\":\"Sniku\",\"rank\":\"Staruszek\",\"avatar\":\"18\"}]";
        return Json(content);
    }

    private static HttpResponseMessage SearchTitle(HttpRequestMessage request)
    {
        var query = request.RequestUri!.Query;
        if (query.Contains("Dorohedoro", StringComparison.OrdinalIgnoreCase))
            return Json("[{\"title_id\":2491,\"type\":\"Manga\",\"title\":\"Dorohedoro\",\"title_status\":\"Zakonczona\",\"cover\":\"173699\"}]");

        if (query.Contains("Bleach", StringComparison.OrdinalIgnoreCase))
            return Json("[{\"title_id\":3,\"type\":\"Anime\",\"title\":\"Bleach\",\"title_status\":\"Zakonczone\",\"cover\":\"3\"}]" );

        return Json("[{\"title_id\":1178,\"type\":\"Anime\",\"title\":\"Gintama\",\"title_status\":\"Zakonczone\",\"cover\":\"235677\"}]" );
    }

    private static HttpResponseMessage CharacterFlatList()
        => Json("[{\"c\":\"1\",\"p\":\"2\",\"f\":\"A\",\"l\":\"B\",\"t\":\"Title\",\"n\":\"3\"}]");

    private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage NotFound(string path) => new(HttpStatusCode.NotFound)
    {
        Content = new StringContent($"{{\"error\":\"Unknown fake endpoint: {path}\"}}", Encoding.UTF8, "application/json")
    };
}
