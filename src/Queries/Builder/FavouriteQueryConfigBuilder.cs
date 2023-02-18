using static Shinden.Queries.Builder.FavouriteQueryConfig;

namespace Shinden.Queries.Builder;

public class FavouriteQueryConfigBuilder
{
    private FavType _type;
    private ulong _userId;
    private ulong _objectId;

    public FavouriteQueryConfigBuilder WithCharacterId(ulong id)
    {
        _type = FavType.Character;
        _objectId = id;
        return this;
    }

    public FavouriteQueryConfigBuilder WithStaffId(ulong id)
    {
        _type = FavType.Staff;
        _objectId = id;
        return this;
    }

    public FavouriteQueryConfigBuilder WithTitleId(ulong id)
    {
        _type = FavType.Title;
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