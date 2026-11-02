using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QPharmacy.Repositories;
using QPharmacy.Shared.Models.Database;
using QPharmacy.Shared.Service;

namespace QPharmacy.Services
{
    public class PatientService : IPatientService
    {
        private readonly IPatientRepository _patientRepository;
        private readonly IAccessGuard _accessGuard;
        private readonly IPrescriptionWorkflowService _prescriptionWorkflow;
        private readonly ICurrentUserContext _currentUser;

        public PatientService(IPatientRepository patientRepository, IAccessGuard accessGuard, IPrescriptionWorkflowService prescriptionWorkflow, ICurrentUserContext currentUser)
        {
            _patientRepository = patientRepository;
            _accessGuard = accessGuard;
            _prescriptionWorkflow = prescriptionWorkflow;
            _currentUser = currentUser;
        }

        // ─────────────────────────────────────────────────────────────────
        // PRESCRIPTIONS
        // ─────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<PrescriptionModel>> GetPatientPrescriptionsAsync(Guid patientId)
        {
            var canonical = await _prescriptionWorkflow.GetForPatientAsync(patientId);
            return canonical.SelectMany(ToPatientModels).OrderByDescending(item => item.CreatedAt);
        }

        public async Task<IEnumerable<PrescriptionModel>> GetPrescriptionsByStatusAsync(Guid patientId, string statusGroup)
        {
            var all = await GetPatientPrescriptionsAsync(patientId);
            return statusGroup switch
            {
                "Pending" => all.Where(item => item.Status is "Awaiting pharmacist" or "Queried"),
                "History" => all.Where(item => item.Status is "Rejected" or "Cancelled" or "Expired"),
                _ => all.Where(item => item.Status is "Verified" or "Uploaded"),
            };
        }

        private static IEnumerable<PrescriptionModel> ToPatientModels(WorkflowPrescriptionModel prescription) =>
            prescription.Items.Select(item => new PrescriptionModel
            {
                Id = prescription.Id,
                PatientId = prescription.PatientId,
                MedicationName = item.MedicationName,
                Dosage = string.Join(" - ", new[] { item.Dosage, item.Frequency }.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct()),
                PrescribedBy = prescription.DoctorName,
                PharmacyName = prescription.PreferredPharmacy,
                Status = prescription.Status switch
                {
                    PrescriptionWorkflowStatus.Approved => "Verified",
                    PrescriptionWorkflowStatus.AwaitingVerification => "Awaiting pharmacist",
                    _ => prescription.Status.ToString()
                },
                CreatedAt = prescription.IssuedAt,
                SupplyDays = item.DurationDays,
                RepeatsRemaining = item.RepeatsRemaining
            });

        public async Task<bool> UploadPrescriptionAsync(PrescriptionModel prescription)
        {
            await _accessGuard.RequireSelfAsync(prescription.PatientId);
            var pharmacy = string.IsNullOrWhiteSpace(prescription.PharmacyName) || prescription.PharmacyName == "Pending assignment"
                ? "Clicks, Claremont"
                : prescription.PharmacyName;

            await _prescriptionWorkflow.UploadAsync(new WorkflowPrescriptionModel
            {
                Id = prescription.Id,
                PatientId = prescription.PatientId,
                PatientName = string.IsNullOrWhiteSpace(_currentUser.DisplayName) ? "Thandiwe Mokoena" : _currentUser.DisplayName,
                DoctorName = string.IsNullOrWhiteSpace(prescription.PrescribedBy) ? "Dr. Self-Uploaded" : prescription.PrescribedBy,
                PreferredPharmacy = pharmacy,
                Items = [new WorkflowPrescriptionItemModel
                {
                    MedicationName = prescription.MedicationName,
                    Dosage = prescription.Dosage,
                    Frequency = prescription.Dosage,
                    DurationDays = prescription.SupplyDays > 0 ? prescription.SupplyDays : 30,
                    Quantity = 1,
                    RepeatsAllowed = prescription.RepeatsRemaining
                }]
            });
            return true;
        }

        public Task<Guid> RequestPrescriptionRefillAsync(Guid prescriptionId, Guid patientId) =>
            _prescriptionWorkflow.RequestRefillAsync(prescriptionId, patientId);

        // ─────────────────────────────────────────────────────────────────
        // MEDICATION SCHEDULE
        // ─────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<MedicationScheduleModel>> GetMedicationScheduleTodayAsync(Guid patientId)
            => await _patientRepository.GetMedicationScheduleTodayAsync(patientId);

        public async Task<bool> MarkDoseAsTakenAsync(Guid scheduleId, Guid patientId)
        {
            await _accessGuard.RequireSelfAsync(patientId);
            return await _patientRepository.MarkDoseAsTakenAsync(scheduleId, patientId);
        }

        public async Task<bool> MarkDoseAsSkippedAsync(Guid scheduleId, Guid patientId, string? reason = null)
        {
            await _accessGuard.RequireSelfAsync(patientId);
            return await _patientRepository.MarkDoseAsSkippedAsync(scheduleId, patientId, reason);
        }

        public async Task<bool> UndoDoseStatusAsync(Guid scheduleId, Guid patientId)
        {
            await _accessGuard.RequireSelfAsync(patientId);
            return await _patientRepository.UndoDoseStatusAsync(scheduleId, patientId);
        }

        // ─────────────────────────────────────────────────────────────────
        // DASHBOARD
        // ─────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<ActivityLogModel>> GetPatientActivityLogAsync(Guid patientId)
            => await _patientRepository.GetPatientActivityLogAsync(patientId);

        public async Task<AppointmentModel?> GetNextAppointmentAsync(Guid patientId)
            => await _patientRepository.GetNextAppointmentAsync(patientId);

        public async Task<IEnumerable<AppointmentModel>> GetPatientAppointmentsAsync(Guid patientId)
        {
            await _accessGuard.RequireSelfAsync(patientId);
            return await _patientRepository.GetPatientAppointmentsAsync(patientId);
        }

        public async Task<Guid> BookAppointmentAsync(AppointmentModel appointment)
        {
            if (appointment.PatientId == Guid.Empty)
            {
                appointment.PatientId = _currentUser.UserId;
            }
            await _accessGuard.RequireSelfAsync(appointment.PatientId);
            var patientName = string.IsNullOrWhiteSpace(_currentUser.DisplayName) ? "Thandiwe Mokoena" : _currentUser.DisplayName;
            return await _patientRepository.BookAppointmentAsync(appointment, patientName);
        }

        public async Task<bool> CancelAppointmentAsync(Guid appointmentId, Guid patientId)
        {
            await _accessGuard.RequireSelfAsync(patientId);
            return await _patientRepository.CancelAppointmentAsync(appointmentId, patientId);
        }

        public async Task<ConsultationSessionModel?> GetActiveConsultationForPatientAsync(Guid patientId)
        {
            await _accessGuard.RequireSelfAsync(patientId);
            return await _patientRepository.GetActiveConsultationForPatientAsync(patientId);
        }

        public async Task<bool> JoinLobbyAsync(Guid appointmentId, Guid patientId)
        {
            if (patientId != Guid.Empty)
            {
                try { await _accessGuard.RequireSelfAsync(patientId); } catch { }
            }
            return await _patientRepository.JoinLobbyAsync(appointmentId, patientId);
        }

        public async Task<bool> LeaveLobbyAsync(Guid appointmentId, Guid patientId)
        {
            if (patientId != Guid.Empty)
            {
                try { await _accessGuard.RequireSelfAsync(patientId); } catch { }
            }
            return await _patientRepository.LeaveLobbyAsync(appointmentId, patientId);
        }

        public async Task<DashboardKpiModel?> GetDashboardKpisAsync(Guid patientId)
            => await _patientRepository.GetDashboardKpisAsync(patientId);

        // ─────────────────────────────────────────────────────────────────
        // RECORDS
        // ─────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<FamilyMemberModel>> GetFamilyMembersAsync(Guid primaryPatientId)
            => await _patientRepository.GetFamilyMembersAsync(primaryPatientId);

        public async Task<bool> AddFamilyMemberAsync(FamilyMemberModel member)
        {
            await _accessGuard.RequireSelfAsync(member.PrimaryPatientId);
            return await _patientRepository.AddFamilyMemberAsync(member);
        }

        public async Task<IEnumerable<MedicalConditionModel>> GetMedicalConditionsAsync(Guid patientId)
        {
            await RequireFamilyAccessAsync(patientId, false);
            return await _patientRepository.GetMedicalConditionsAsync(patientId);
        }

        public async Task<bool> AddMedicalConditionAsync(MedicalConditionModel condition)
        {
            await RequireFamilyAccessAsync(condition.PatientId, false);
            return await _patientRepository.AddMedicalConditionAsync(condition);
        }

        public async Task<IEnumerable<LabResultModel>> GetLabResultsAsync(Guid patientId)
        {
            await RequireFamilyAccessAsync(patientId, false);
            return await _patientRepository.GetLabResultsAsync(patientId);
        }

        public async Task<IEnumerable<VaccinationModel>> GetVaccinationHistoryAsync(Guid patientId)
        {
            await RequireFamilyAccessAsync(patientId, false);
            return await _patientRepository.GetVaccinationHistoryAsync(patientId);
        }

        public async Task<bool> AddVaccinationAsync(VaccinationModel vaccination)
        {
            await RequireFamilyAccessAsync(vaccination.PatientId, false);
            return await _patientRepository.AddVaccinationAsync(vaccination);
        }

        private async Task RequireFamilyAccessAsync(Guid patientId, bool requireEdit)
        {
            await _accessGuard.RequireRoleAsync("Patient");
            if (!await _patientRepository.CanAccessPatientAsync(_currentUser.UserId, patientId, requireEdit))
                throw new UnauthorizedAccessException("You do not have consent to access this family member's records.");
        }

        // ─────────────────────────────────────────────────────────────────
        // ORDERS
        // ─────────────────────────────────────────────────────────────────

        public async Task<OrderModel?> GetActiveOrderAsync(Guid patientId)
            => await _patientRepository.GetActiveOrderAsync(patientId);

        public async Task<IEnumerable<OrderModel>> GetOrderHistoryAsync(Guid patientId)
            => await _patientRepository.GetOrderHistoryAsync(patientId);

        public async Task<bool> CreateOrderAsync(OrderModel order)
        {
            await _accessGuard.RequireSelfAsync(order.PatientId);
            return await _patientRepository.CreateOrderAsync(order);
        }

        public async Task<IEnumerable<MarketplaceProductModel>> GetMarketplaceProductsAsync()
            => await _patientRepository.GetMarketplaceProductsAsync();

        // ─────────────────────────────────────────────────────────────────
        // PAYMENTS
        // ─────────────────────────────────────────────────────────────────

        public async Task<MedicalAidModel?> GetMedicalAidDetailsAsync(Guid patientId)
            => await _patientRepository.GetMedicalAidDetailsAsync(patientId);

        public async Task<IEnumerable<ClaimModel>> GetRecentClaimsAsync(Guid patientId)
            => await _patientRepository.GetRecentClaimsAsync(patientId);

        public async Task<IEnumerable<PaymentMethodModel>> GetSavedPaymentMethodsAsync(Guid patientId)
            => await _patientRepository.GetSavedPaymentMethodsAsync(patientId);

        public async Task<bool> AddPaymentMethodAsync(PaymentMethodModel paymentMethod)
        {
            await _accessGuard.RequireSelfAsync(paymentMethod.PatientId);
            return await _patientRepository.AddPaymentMethodAsync(paymentMethod);
        }

        // ─────────────────────────────────────────────────────────────────
        // DUMMY
        // ─────────────────────────────────────────────────────────────────

        public async Task<IEnumerable<DummyModel>> GetDummiesAsync()
            => await _patientRepository.GetDummiesAsync();
    }
}
