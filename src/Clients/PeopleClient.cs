using PeopleQueries = ShindenAPI.Queries.People;
using System.Threading;
using System.Threading.Tasks;
using ShindenAPI.Models;


namespace ShindenAPI;

public sealed class PeopleClient
{
    private readonly RequestManager _legacyManager;
    internal PeopleClient(RequestManager legacyManager) => _legacyManager = legacyManager;

    public Task<ErrorOr<StaffInfo>> StaffInfoAsync(
        ulong staffId, CancellationToken cancellationToken = default)
        => _legacyManager.MakeQueryAsync(new PeopleQueries.StaffInfo(staffId), cancellationToken: cancellationToken);
}
