using System.Threading;
using System.Threading.Tasks;
using ShindenAPI.Models;
using PeopleQueries = ShindenAPI.Queries.People;

namespace ShindenAPI;

/// <summary>Provides staff and character operations.</summary>
public sealed class PeopleClient
{
    private readonly RequestManager _manager;

    internal PeopleClient(RequestManager manager) => _manager = manager;

    public Task<ErrorOr<StaffInfo>> StaffInfoAsync(
        ulong staffId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new PeopleQueries.StaffInfo(staffId), cancellationToken: cancellationToken);

    public Task<ErrorOr<CharacterInfo>> CharacterInfoAsync(
        ulong characterId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new PeopleQueries.CharacterInfo(characterId), cancellationToken: cancellationToken);
}
