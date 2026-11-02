using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QPharmacy.Shared.Models.Database;

namespace QPharmacy.Services
{
    public interface IPharmacistService
    {
        // --- Inventory ---
        Task<IEnumerable<PharmacyInventoryModel>> GetInventoryAsync(string branchName = "Clicks, Claremont");
        Task<bool> AddInventoryStockAsync(PharmacyInventoryModel item);
        Task<bool> RestockItemAsync(Guid id, int addQuantity);

        // --- Verification Queue ---
        Task<IEnumerable<VerificationQueueModel>> GetVerificationQueueAsync();
        Task<bool> UpdateVerificationStatusAsync(Guid id, string status, string? queryNote = null);

        // --- Dispensing Orders ---
        Task<IEnumerable<DispensingOrderModel>> GetDispensingOrdersAsync();
        Task<bool> UpdateDispensingOrderAsync(Guid id, string actionLabel, string handoverRef);
        Task<bool> AddDispensingOrderAsync(DispensingOrderModel order);
        Task<IEnumerable<FulfilmentItemModel>> GetFulfilmentItemsAsync(Guid orderId);
        Task<bool> SetPartialFulfilmentAsync(Guid orderId, Guid itemId, int quantity);
        Task<bool> SubstituteMedicationAsync(Guid orderId, Guid itemId, string medicationName, string reason);
        Task<bool> CancelFulfilmentAsync(Guid orderId, string reason);

        // --- Dashboard / Reports ---
        Task<PharmacistKpiModel> GetPharmacistKpisAsync();
    }
}
