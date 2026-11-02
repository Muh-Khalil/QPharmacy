using QPharmacy.Repositories;
using QPharmacy.Shared.Models.Database;
using QPharmacy.Shared.Service;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QPharmacy.Services
{
    public class PharmacistService : IPharmacistService
    {
        private readonly IPharmacistRepository _pharmacistRepository;
        private readonly IAccessGuard _accessGuard;
        private readonly IPrescriptionWorkflowService _prescriptionWorkflow;
        private readonly IFulfilmentRepository _fulfilmentRepository;
        private readonly ICurrentUserContext _currentUser;

        public PharmacistService(IPharmacistRepository pharmacistRepository, IAccessGuard accessGuard,
            IPrescriptionWorkflowService prescriptionWorkflow, IFulfilmentRepository fulfilmentRepository,
            ICurrentUserContext currentUser)
        {
            _pharmacistRepository = pharmacistRepository;
            _accessGuard = accessGuard;
            _prescriptionWorkflow = prescriptionWorkflow;
            _fulfilmentRepository = fulfilmentRepository;
            _currentUser = currentUser;
        }

        public async Task<IEnumerable<PharmacyInventoryModel>> GetInventoryAsync(string branchName = "Clicks, Claremont")
        {
            await _accessGuard.RequireRoleAsync("Pharmacist");
            if (string.IsNullOrWhiteSpace(_currentUser.OrganizationOrBranch))
                throw new UnauthorizedAccessException("The pharmacist account is not assigned to a pharmacy branch.");
            return await _pharmacistRepository.GetInventoryAsync(_currentUser.OrganizationOrBranch);
        }

        public async Task<bool> AddInventoryStockAsync(PharmacyInventoryModel item)
        {
            await _accessGuard.RequireRoleAsync("Pharmacist");
            item.BranchName = _currentUser.OrganizationOrBranch;
            return await _pharmacistRepository.AddInventoryStockAsync(item);
        }

        public async Task<bool> RestockItemAsync(Guid id, int addQuantity)
        {
            await _accessGuard.RequireRoleAsync("Pharmacist");
            if (addQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(addQuantity));
            var inventory = await GetInventoryAsync();
            if (!inventory.Any(item => item.Id == id))
                throw new UnauthorizedAccessException("This inventory item belongs to another pharmacy branch.");
            return await _pharmacistRepository.RestockItemAsync(id, addQuantity);
        }

        public async Task<IEnumerable<VerificationQueueModel>> GetVerificationQueueAsync()
        {
            var canonical = await _prescriptionWorkflow.GetVerificationQueueAsync();
            var mapped = canonical.Select(item => new VerificationQueueModel
            {
                Id = item.Id,
                PrescriptionNumber = item.PrescriptionNumber,
                PatientName = item.PatientName,
                DoctorName = item.DoctorName,
                MedicationDetails = string.Join("; ", item.Items.Select(medication => $"{medication.MedicationName} {medication.Dosage}")),
                MedicationShort = string.Join(", ", item.Items.Select(medication => medication.MedicationName)),
                Status = item.Status == PrescriptionWorkflowStatus.AwaitingVerification ? "Pending" : item.Status.ToString(),
                CreatedAt = item.IssuedAt,
                Initials = string.Concat(item.PatientName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => char.ToUpperInvariant(part[0])))
            });
            return mapped.OrderBy(item => item.CreatedAt);
        }

        public async Task<bool> UpdateVerificationStatusAsync(Guid id, string status, string? queryNote = null)
        {
            await _accessGuard.RequireRoleAsync("Pharmacist");
            var canonical = await _prescriptionWorkflow.GetVerificationQueueAsync();
            if (canonical.Any(item => item.Id == id))
            {
                if (status == "Approved") await _prescriptionWorkflow.ApproveAsync(id);
                else if (status == "Queried") await _prescriptionWorkflow.QueryDoctorAsync(id, queryNote ?? string.Empty);
                else if (status == "Rejected") await _prescriptionWorkflow.RejectAsync(id, queryNote ?? "Rejected by pharmacist");
                else throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported prescription status.");
                return true;
            }
            return await _pharmacistRepository.UpdateVerificationStatusAsync(id, status, queryNote);
        }

        public async Task<IEnumerable<DispensingOrderModel>> GetDispensingOrdersAsync()
        {
            await _accessGuard.RequireRoleAsync("Pharmacist");
            if (string.IsNullOrWhiteSpace(_currentUser.OrganizationOrBranch))
                throw new UnauthorizedAccessException("The pharmacist account is not assigned to a pharmacy branch.");
            return await _pharmacistRepository.GetDispensingOrdersAsync(_currentUser.OrganizationOrBranch);
        }

        public async Task<bool> UpdateDispensingOrderAsync(Guid id, string actionLabel, string handoverRef)
        {
            await _accessGuard.RequireRoleAsync("Pharmacist");
            await RequireOrderBranchAsync(id);
            return await _pharmacistRepository.UpdateDispensingOrderAsync(id, actionLabel, handoverRef);
        }

        public async Task<bool> AddDispensingOrderAsync(DispensingOrderModel order)
        {
            await _accessGuard.RequireRoleAsync("Pharmacist");
            return await _pharmacistRepository.AddDispensingOrderAsync(order);
        }

        public Task<PharmacistKpiModel> GetPharmacistKpisAsync()
        {
            return _pharmacistRepository.GetPharmacistKpisAsync(_currentUser.OrganizationOrBranch);
        }

        public async Task<IEnumerable<FulfilmentItemModel>> GetFulfilmentItemsAsync(Guid orderId)
        {
            await RequireOrderBranchAsync(orderId);
            return await _fulfilmentRepository.GetItemsAsync(orderId);
        }

        public async Task<bool> SetPartialFulfilmentAsync(Guid orderId, Guid itemId, int quantity)
        {
            await RequireOrderBranchAsync(orderId);
            return await _fulfilmentRepository.SetFulfilledQuantityAsync(itemId, quantity, _currentUser.UserId);
        }

        public async Task<bool> SubstituteMedicationAsync(Guid orderId, Guid itemId, string medicationName, string reason)
        {
            await RequireOrderBranchAsync(orderId);
            if (string.IsNullOrWhiteSpace(medicationName) || string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A substitution and clinical reason are required.");
            return await _fulfilmentRepository.SubstituteAsync(itemId, medicationName.Trim(), reason.Trim(), _currentUser.UserId);
        }

        public async Task<bool> CancelFulfilmentAsync(Guid orderId, string reason)
        {
            await RequireOrderBranchAsync(orderId);
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A cancellation reason is required.");
            return await _fulfilmentRepository.CancelAndReleaseAsync(orderId, reason.Trim(), _currentUser.UserId);
        }

        private async Task RequireOrderBranchAsync(Guid orderId)
        {
            await _accessGuard.RequireRoleAsync("Pharmacist");
            var branch = await _fulfilmentRepository.GetOrderBranchAsync(orderId);
            if (string.IsNullOrWhiteSpace(branch) || !string.Equals(branch, _currentUser.OrganizationOrBranch, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("This order belongs to another pharmacy branch.");
        }
    }
}
