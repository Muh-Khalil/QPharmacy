using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QPharmacy.Shared.Models.Database;

namespace QPharmacy.Repositories
{
    public interface IPharmacistRepository
    {
        // --- Inventory ---
        Task<IEnumerable<PharmacyInventoryModel>> GetInventoryAsync(string branchName = "Clicks, Claremont");
        Task<bool> AddInventoryStockAsync(PharmacyInventoryModel item);
        Task<bool> RestockItemAsync(Guid id, int addQuantity);

        // --- Verification Queue ---
        Task<IEnumerable<VerificationQueueModel>> GetVerificationQueueAsync();
        Task<bool> UpdateVerificationStatusAsync(Guid id, string status, string? queryNote = null);

        // --- Dispensing Orders ---
        Task<IEnumerable<DispensingOrderModel>> GetDispensingOrdersAsync(string branchName);
        Task<bool> UpdateDispensingOrderAsync(Guid id, string actionLabel, string handoverRef);
        Task<bool> AddDispensingOrderAsync(DispensingOrderModel order);

        // --- Dashboard / Reports ---
        Task<PharmacistKpiModel> GetPharmacistKpisAsync(string? branchName = null);
    }
}
