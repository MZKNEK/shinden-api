using System.Net.Http;
using System.Text;

namespace ShindenAPI.Queries.Builder;

/// <summary>Canonical payload used to add or remove a favourite.</summary>
public class FavouriteQueryConfig
{
    /// <summary>Supported favourite entity types.</summary>
    public enum FavType
    {
        Character, Title, Staff
    }

    public required FavType Type { get; init; }
    public required ulong UserId { get; init; }
    public required ulong ObjectId { get; init; }

    internal HttpContent Build()
    {
        return new StringContent($"id={Type.ToString().ToLower()}-{ObjectId}",
            Encoding.UTF8, "application/x-www-form-urlencoded");
    }
}

    /// <summary>Builds a <see cref="FavouriteQueryConfig"/>.</summary>
public class FavouriteQueryConfigBuilder
{
    private FavouriteQueryConfig.FavType _type;
    private ulong _userId;
    private ulong _objectId;

    public FavouriteQueryConfigBuilder WithCharacterId(ulong id)
    {
        _type = FavouriteQueryConfig.FavType.Character;
        _objectId = id;
        return this;
    }

    public FavouriteQueryConfigBuilder WithStaffId(ulong id)
    {
        _type = FavouriteQueryConfig.FavType.Staff;
        _objectId = id;
        return this;
    }

    public FavouriteQueryConfigBuilder WithTitleId(ulong id)
    {
        _type = FavouriteQueryConfig.FavType.Title;
        _objectId = id;
        return this;
    }

    public FavouriteQueryConfigBuilder WithUserId(ulong id)
    {
        _userId = id;
        return this;
    }

    public FavouriteQueryConfig Build() => new FavouriteQueryConfig
    {
        ObjectId = _objectId,
        UserId = _userId,
        Type = _type
    };
}
