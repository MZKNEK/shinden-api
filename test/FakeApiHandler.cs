using System.Net;
using System.Net.Http;
using System.Text;

namespace ShindenAPI.Test;

internal sealed class FakeApiHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath.Trim('/');
        var response = path switch
        {
            "v1/search/users" => Json("""[{"user_id":85,"name":"Sniku","rank":"Staruszek","avatar":true}]"""),
            "v1/search/staff" => Json("""[{"staff_id":156,"first_name":"Marina","last_name":"Inoue","all_names":"Marina Inoue","picture":176530}]"""),
            "v1/search/characters" => Json("""[{"character_id":663,"first_name":"Gintoki","last_name":"Sakata","all_names":"Gintoki Sakata","picture":274727,"title":"Gintama","dokladnie":true}]"""),
            "v1/search/titles" => SearchTitle(request),
            "compat/php-api/character/flat-list" or "compat/php-api/character/flat-list-anime"
                => Json("""[{"c":1,"p":2,"f":"A","l":"B","t":"Title","n":120}]"""),
            "compat/php-api/anime/current-season" => Json("""{"season":{"year":2026,"season":"fall"},"updated_at":"2026-11-07 12:00:00","titles":[101,102],"characters":["201","202"],"character_titles":[{"character_id":"201","title_id":101}]}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent($"Unknown fake endpoint: {path}") }
        };
        return Task.FromResult(response);
    }

    private static HttpResponseMessage SearchTitle(HttpRequestMessage request)
    {
        var query = request.RequestUri!.Query;
        var (id, name, type, cover) = query.Contains("Dorohedoro", StringComparison.OrdinalIgnoreCase)
            ? (2491, "Dorohedoro", "Manga", 173699) : query.Contains("Bleach", StringComparison.OrdinalIgnoreCase)
            ? (3, "Bleach", "Anime", 3) : (1178, "Gintama", "Anime", 235677);
        return Json($$"""[{"title_id":{{id}},"title":"{{name}}","type":"{{type}}","cover":{{cover}},"title_url":"https://shinden.pl/titles/{{id}}-example"}]""");
    }

    private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };
}
