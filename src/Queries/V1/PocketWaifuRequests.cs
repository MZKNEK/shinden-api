using System;
using System.Collections.Generic;
using System.Linq;
using ShindenAPI.Models;

namespace ShindenAPI.Queries.V1;

/// <summary>Gallery filters shared by card pages and the matching ID projection.</summary>
public sealed class PwCardOptions
{
    public string Query { get; init; }
    public string Rarity { get; init; }
    public bool? Ultimate { get; init; }
    public bool? Scalpel { get; init; }
    public bool? Cage { get; init; }
    public bool? Tradable { get; init; }
    public bool? Expedition { get; init; }
    public bool? Cursed { get; init; }
    public string Dere { get; init; }
    public string Genre { get; init; }
    public string Tags { get; init; }
    public string ExcludedTags { get; init; }
    public string TagMode { get; init; }
    public bool? Untagged { get; init; }
    public string CardIds { get; init; }
    public string Sort { get; init; }
    public string Direction { get; init; }
    public int Limit { get; init; } = 48;
    public string Cursor { get; init; }

    internal KeyValuePair<string, string>[] Parameters()
    {
        if (Limit is not (48 or 100 or 200)) throw new ArgumentOutOfRangeException(nameof(Limit));
        return new[]
        {
            RequestUri.Parameter("q", Query), RequestUri.Parameter("rarity", Rarity),
            Flag("ultimate", Ultimate), Flag("scalpel", Scalpel), Flag("cage", Cage), Flag("tradable", Tradable),
            Flag("expedition", Expedition), Flag("cursed", Cursed), Flag("untagged", Untagged),
            RequestUri.Parameter("dere", Dere), RequestUri.Parameter("genre", Genre),
            RequestUri.Parameter("tags", Tags), RequestUri.Parameter("xtags", ExcludedTags),
            RequestUri.Parameter("tagmode", TagMode), RequestUri.Parameter("wid", CardIds),
            RequestUri.Parameter("sort", Sort), RequestUri.Parameter("dir", Direction),
            RequestUri.Parameter("limit", Limit), RequestUri.Parameter("cursor", Cursor)
        };
    }

    private static KeyValuePair<string, string> Flag(string key, bool? value)
        => RequestUri.Parameter(key, value.HasValue ? (value.Value ? "1" : "0") : null);
}

public sealed class UserCards : GetRequest<PwCardsPage>
{
    public UserCards(ulong userId, PwCardOptions options = null)
        : base($"v1/users/{userId}/pw/cards", (options ?? new PwCardOptions()).Parameters()) { }
}

public sealed class UserCardIds : GetRequest<PwCardIds>
{
    public UserCardIds(ulong userId, PwCardOptions options = null)
        : base($"v1/users/{userId}/pw/cards/ids", (options ?? new PwCardOptions()).Parameters()
            .Where(p => p.Key != "cursor" && p.Key != "limit").ToArray()) { }
}

public sealed class UserCardTags : GetRequest<PwTags>
{
    public UserCardTags(ulong userId) : base($"v1/users/{userId}/pw/tags") { }
}

public sealed class CardGenres : GetRequest<PwGenres>
{
    public CardGenres() : base("v1/pw/genres") { }
}

public sealed class Card : GetRequest<PwCardDetails>
{
    public Card(ulong cardId) : base($"v1/pw/cards/{cardId}") { }
}
