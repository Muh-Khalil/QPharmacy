using QPharmacy.Shared.Models.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QPharmacy.Repositories
{
    public class MockPharmacistRepository : IPharmacistRepository
    {
        private readonly List<PharmacyInventoryModel> _inventory = new()
        {
            new PharmacyInventoryModel { Id = Guid.NewGuid(), BranchName = "Clicks, Claremont", MedicationName = "Amlodipine 5mg", InStock = 420, ReorderLevel = 100, Status = "Healthy" },
            new PharmacyInventoryModel { Id = Guid.NewGuid(), BranchName = "Clicks, Claremont", MedicationName = "Metformin 500mg", InStock = 85, ReorderLevel = 100, Status = "Low" },
            new PharmacyInventoryModel { Id = Guid.NewGuid(), BranchName = "Clicks, Claremont", MedicationName = "Atorvastatin 20mg", InStock = 12, ReorderLevel = 50, Status = "Reorder now" },
            new PharmacyInventoryModel { Id = Guid.NewGuid(), BranchName = "Clicks, Claremont", MedicationName = "Cetirizine 10mg", InStock = 310, ReorderLevel = 80, Status = "Healthy" }
        };

        private static readonly List<VerificationQueueModel> _verificationQueue = new()
        {
            new VerificationQueueModel
            {
                Id = Guid.NewGuid(),
                PrescriptionNumber = "4821",
                PatientName = "Thandiwe Mokoena",
                DoctorName = "Dr. N. Adams",
                MedicationDetails = "Atorvastatin 20mg, nightly, 30 days",
                InteractionStatus = "No conflicts detected",
                IsFlagged = false,
                Initials = "TM",
                AvatarBgColor = "bg-teal-600",
                MedicationShort = "Atorvastatin 20mg",
                Status = "Pending"
            },
            new VerificationQueueModel
            {
                Id = Guid.NewGuid(),
                PrescriptionNumber = "4822",
                PatientName = "Thandiwe Mokoena",
                DoctorName = "Dr. N. Adams",
                MedicationDetails = "Ibuprofen 200mg, twice daily, 10 days",
                InteractionStatus = "Conflict detected: Combined with Amlodipine",
                IsFlagged = true,
                Initials = "TM",
                AvatarBgColor = "bg-rose-600",
                MedicationShort = "Ibuprofen 200mg (Flagged)",
                Status = "Pending"
            },
            new VerificationQueueModel
            {
                Id = Guid.NewGuid(),
                PrescriptionNumber = "4823",
                PatientName = "Jabu Khumalo",
                DoctorName = "Dr. S. Patel",
                MedicationDetails = "Metformin 500mg, twice daily, 30 days",
                InteractionStatus = "No conflicts detected",
                IsFlagged = false,
                Initials = "JK",
                AvatarBgColor = "bg-blue-600",
                MedicationShort = "Metformin 500mg",
                Status = "Pending"
            },
            new VerificationQueueModel
            {
                Id = Guid.NewGuid(),
                PrescriptionNumber = "4824",
                PatientName = "Rose Pillay",
                DoctorName = "Dr. M. Botha",
                MedicationDetails = "Bisoprolol 5mg, once daily, 30 days",
                InteractionStatus = "No conflicts detected",
                IsFlagged = false,
                Initials = "RP",
                AvatarBgColor = "bg-amber-500",
                MedicationShort = "Bisoprolol 5mg",
                Status = "Pending"
            }
        };

        private readonly List<DispensingOrderModel> _dispensingOrders = new()
        {
            new DispensingOrderModel
            {
                Id = Guid.NewGuid(),
                OrderNumber = "QP-1042",
                PatientName = "Thandiwe Mokoena",
                ItemCount = 3,
                Fulfilment = "Delivery",
                HandoverRef = "Courier: Fastway (TRK-88412)",
                ActionLabel = "Mark packed"
            },
            new DispensingOrderModel
            {
                Id = Guid.NewGuid(),
                OrderNumber = "QP-1043",
                PatientName = "Jabu Khumalo",
                ItemCount = 1,
                Fulfilment = "Click & collect",
                HandoverRef = "Pickup Code: #PIN-4810",
                ActionLabel = "Mark ready"
            }
        };

        public Task<IEnumerable<PharmacyInventoryModel>> GetInventoryAsync(string branchName = "Clicks, Claremont")
        {
            return Task.FromResult<IEnumerable<PharmacyInventoryModel>>(_inventory);
        }

        public Task<bool> AddInventoryStockAsync(PharmacyInventoryModel item)
        {
            _inventory.Insert(0, item);
            return Task.FromResult(true);
        }

        public Task<bool> RestockItemAsync(Guid id, int addQuantity)
        {
            var match = _inventory.FirstOrDefault(x => x.Id == id);
            if (match != null)
            {
                match.InStock += addQuantity;
                if (match.InStock > match.ReorderLevel)
                {
                    match.Status = "Healthy";
                }
                match.UpdatedAt = DateTime.UtcNow;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<IEnumerable<VerificationQueueModel>> GetVerificationQueueAsync()
        {
            return Task.FromResult<IEnumerable<VerificationQueueModel>>(_verificationQueue.Where(x => x.Status == "Pending"));
        }

        public Task<bool> UpdateVerificationStatusAsync(Guid id, string status, string? queryNote = null)
        {
            var item = _verificationQueue.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                item.Status = status;
                if (status == "Approved")
                {
                    MockPatientRepository.UpdatePrescriptionStatus(id, "Verified");
                    MockDoctorRepository.UpdatePrescriptionStatus(id, "Verified");
                }
                else if (status == "Queried")
                {
                    MockPatientRepository.UpdatePrescriptionStatus(id, "Queried");
                    MockDoctorRepository.UpdatePrescriptionStatus(id, "Queried");
                }
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        internal static void AddMockVerificationQueueItem(VerificationQueueModel item)
        {
            _verificationQueue.Insert(0, item);
        }

        public Task<IEnumerable<DispensingOrderModel>> GetDispensingOrdersAsync(string branchName)
        {
            return Task.FromResult<IEnumerable<DispensingOrderModel>>(_dispensingOrders);
        }

        public Task<bool> UpdateDispensingOrderAsync(Guid id, string actionLabel, string handoverRef)
        {
            var item = _dispensingOrders.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                item.ActionLabel = actionLabel;
                item.HandoverRef = handoverRef;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> AddDispensingOrderAsync(DispensingOrderModel order)
        {
            _dispensingOrders.Insert(0, order);
            return Task.FromResult(true);
        }

        public Task<PharmacistKpiModel> GetPharmacistKpisAsync(string? branchName = null)
        {
            var kpis = new PharmacistKpiModel
            {
                AwaitingVerificationCount = _verificationQueue.Count(x => x.Status == "Pending"),
                ReadyForCollectionCount = _dispensingOrders.Count(x => x.ActionLabel == "Mark ready" || x.ActionLabel == "Patient Notified"),
                LowStockCount = _inventory.Count(x => x.Status != "Healthy"),
                DispensedTodayCount = _dispensingOrders.Count + 40
            };
            return Task.FromResult(kpis);
        }
    }
}
