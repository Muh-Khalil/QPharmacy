using QPharmacy.Shared.Models.Database;

namespace QPharmacy.Repositories;

public sealed class MockFulfilmentRepository : IFulfilmentRepository
{
    public Task<string?> GetOrderBranchAsync(Guid orderId) => Task.FromResult<string?>(null);
    public Task<IEnumerable<FulfilmentItemModel>> GetItemsAsync(Guid orderId) => Task.FromResult<IEnumerable<FulfilmentItemModel>>([]);
    public Task<bool> SetFulfilledQuantityAsync(Guid itemId, int quantity, Guid actorUserId) => Task.FromResult(false);
    public Task<bool> SubstituteAsync(Guid itemId, string medicationName, string reason, Guid actorUserId) => Task.FromResult(false);
    public Task<bool> CancelAndReleaseAsync(Guid orderId, string reason, Guid actorUserId) => Task.FromResult(false);
}
