using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ShindenAPI.Models;

internal sealed class PhpTitleRowConverter : JsonConverter
{
    public PhpTitleRowConverter() { }
    public override bool CanWrite => false;
    public override bool CanConvert(Type objectType) => objectType == typeof(Review) || objectType == typeof(Recommendation);

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        var row = JObject.Load(reader);
        Alias(row, "id", objectType == typeof(Review) ? "review_id" : "recommendation_id");
        Alias(row, "content", objectType == typeof(Review) ? "review" : "recommendation");
        if (row.Property("author") is null && row.Property("user_id") is not null)
            row["author"] = new JObject
            {
                ["user_id"] = row["user_id"]?.DeepClone(),
                ["name"] = row["name"]?.DeepClone(),
                ["avatar"] = row["avatar"]?.DeepClone()
            };

        object result;
        if (objectType == typeof(Review))
        {
            Alias(row, "read_count", "read_cnt");
            Alias(row, "enter_count", "enter_cnt");
            // PHP database flags are strings; retain support for typed booleans too.
            var flag = row["is_abstract"];
            if (flag?.Type == JTokenType.String || flag?.Type == JTokenType.Integer)
            {
                if (flag.ToString() == "0") row["is_abstract"] = false;
                else if (flag.ToString() == "1") row["is_abstract"] = true;
            }
            result = new Review();
        }
        else
        {
            Alias(row, "base_title_id", "rec_title_id");
            if (row.Property("recommended_title") is null && row.Property("rtitle_id") is not null)
                row["recommended_title"] = new JObject
                {
                    ["title_id"] = row["rtitle_id"]?.DeepClone(),
                    ["title"] = row["title"]?.DeepClone(),
                    ["cover"] = row["cover_artifact_id"]?.DeepClone()
                };
            result = new Recommendation();
        }

        using var normalized = row.CreateReader();
        serializer.Populate(normalized, result);
        return result;
    }

    private static void Alias(JObject row, string target, string source)
    {
        if (row.Property(target) is null && row.Property(source) is not null)
            row[target] = row[source].DeepClone();
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        => throw new NotSupportedException();
}
