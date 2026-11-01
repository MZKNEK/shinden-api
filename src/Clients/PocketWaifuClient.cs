using System.Threading;
using System.Threading.Tasks;
using ShindenAPI.Models;
using ShindenAPI.Queries.V1;

namespace ShindenAPI;

/// <summary>Reads the public Pocket Waifu copy; cursors come from PwCardsPage.NextCursor.</summary>
public sealed class PocketWaifuClient
{
    private readonly RequestManager _manager;
    internal PocketWaifuClient(RequestManager manager) => _manager = manager;

    public Task<ErrorOr<PwCardsPage>> CardsAsync(ulong userId, PwCardOptions options = null, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new UserCards(userId, options), cancellationToken);

    public Task<ErrorOr<PwCardIds>> CardIdsAsync(ulong userId, PwCardOptions options = null, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new UserCardIds(userId, options), cancellationToken);

    public Task<ErrorOr<PwTags>> TagsAsync(ulong userId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new UserCardTags(userId), cancellationToken);

    public Task<ErrorOr<PwGenres>> GenresAsync(CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new CardGenres(), cancellationToken);

    public Task<ErrorOr<PwCardDetails>> CardAsync(ulong cardId, CancellationToken cancellationToken = default)
        => _manager.MakeQueryAsync(new Card(cardId), cancellationToken);
}
