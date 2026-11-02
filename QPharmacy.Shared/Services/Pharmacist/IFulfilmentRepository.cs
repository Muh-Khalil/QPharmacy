using QPharmacy.Shared.Models.Database;

namespace QPharmacy.Repositories;

public interface IFulfilmentRepository
{
    Task<string?> GetOrderBranchAsync(Guid orderId);
    Task<IEnumerable<FulfilmentItemModel>> GetItemsAsync(Guid orderId);
    Task<bool> SetFulfilledQuantityAsync(Guid itemId, int quantity, Guid actorUserId);
    Task<bool> SubstituteAsync(Guid itemId, string medicationName, string reason, Guid actorUserId);
    Task<bool> CancelAndReleaseAsync(Guid orderId, string reason, Guid actorUserId);
}
