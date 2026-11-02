using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QPharmacy.Shared.Models.Database;

namespace QPharmacy.Services
{
    public interface IPatientService
    {
        // --- Prescriptions ---
        Task<IEnumerable<PrescriptionModel>> GetPatientPrescriptionsAsync(Guid patientId);
        Task<IEnumerable<PrescriptionModel>> GetPrescriptionsByStatusAsync(Guid patientId, string statusGroup); // "Active" | "History" | "Pending"
        Task<bool> UploadPrescriptionAsync(PrescriptionModel prescription);
        Task<Guid> RequestPrescriptionRefillAsync(Guid prescriptionId, Guid patientId);

        // --- Medication Schedule ---
        Task<IEnumerable<MedicationScheduleModel>> GetMedicationScheduleTodayAsync(Guid patientId);
        Task<bool> MarkDoseAsTakenAsync(Guid scheduleId, Guid patientId);
        Task<bool> MarkDoseAsSkippedAsync(Guid scheduleId, Guid patientId, string? reason = null);
        Task<bool> UndoDoseStatusAsync(Guid scheduleId, Guid patientId);

        // --- Dashboard & Appointments ---
        Task<IEnumerable<ActivityLogModel>> GetPatientActivityLogAsync(Guid patientId);
        Task<AppointmentModel?> GetNextAppointmentAsync(Guid patientId);
        Task<IEnumerable<AppointmentModel>> GetPatientAppointmentsAsync(Guid patientId);
        Task<Guid> BookAppointmentAsync(AppointmentModel appointment);
        Task<bool> CancelAppointmentAsync(Guid appointmentId, Guid patientId);
        Task<ConsultationSessionModel?> GetActiveConsultationForPatientAsync(Guid patientId);
        Task<bool> JoinLobbyAsync(Guid appointmentId, Guid patientId);
        Task<bool> LeaveLobbyAsync(Guid appointmentId, Guid patientId);
        Task<DashboardKpiModel?> GetDashboardKpisAsync(Guid patientId);

        // --- Records ---
        Task<IEnumerable<FamilyMemberModel>> GetFamilyMembersAsync(Guid primaryPatientId);
        Task<bool> AddFamilyMemberAsync(FamilyMemberModel member);
        Task<IEnumerable<MedicalConditionModel>> GetMedicalConditionsAsync(Guid patientId);
        Task<bool> AddMedicalConditionAsync(MedicalConditionModel condition);
        Task<IEnumerable<LabResultModel>> GetLabResultsAsync(Guid patientId);
        Task<IEnumerable<VaccinationModel>> GetVaccinationHistoryAsync(Guid patientId);
        Task<bool> AddVaccinationAsync(VaccinationModel vaccination);

        // --- Orders ---
        Task<OrderModel?> GetActiveOrderAsync(Guid patientId);
        Task<IEnumerable<OrderModel>> GetOrderHistoryAsync(Guid patientId);
        Task<bool> CreateOrderAsync(OrderModel order);
        Task<IEnumerable<MarketplaceProductModel>> GetMarketplaceProductsAsync();

        // --- Payments ---
        Task<MedicalAidModel?> GetMedicalAidDetailsAsync(Guid patientId);
        Task<IEnumerable<ClaimModel>> GetRecentClaimsAsync(Guid patientId);
        Task<IEnumerable<PaymentMethodModel>> GetSavedPaymentMethodsAsync(Guid patientId);
        Task<bool> AddPaymentMethodAsync(PaymentMethodModel paymentMethod);

        // --- Dummy ---
        Task<IEnumerable<DummyModel>> GetDummiesAsync();
    }
}
