using QPharmacy.Shared.Models.Database;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QPharmacy.Services
{
    public interface IDoctorService
    {
        Task<DoctorKpiModel> GetDoctorKpisAsync(Guid doctorId);
        Task<IEnumerable<DoctorScheduleItemModel>> GetTodaysScheduleAsync(Guid doctorId);
        Task<IEnumerable<DoctorPendingActionModel>> GetPendingActionsAsync(Guid doctorId);
        Task<IEnumerable<DoctorPatientModel>> GetDoctorPatientsAsync(Guid doctorId, string? searchQuery = null);
        Task<bool> AddPatientAsync(DoctorPatientModel patient);
        Task<ConsultationSessionModel> GetConsultationSessionAsync(Guid patientId);
        Task<bool> SaveClinicalNoteAndReferralAsync(Guid consultationId, string assessment, string plan, bool createReferral);
        Task<bool> CompleteConsultationAsync(Guid consultationId, string assessment, string plan, string? referralNote, DoctorPrescriptionModel? script = null);
        Task<ConsultationSessionModel?> GetConsultationSessionByIdAsync(Guid consultationId);
        Task<ConsultationSessionModel?> GetConsultationSessionByAppointmentIdAsync(Guid appointmentId);
        Task<bool> SendChatMessageAsync(Guid consultationId, string senderName, string text, bool isDoctor, Guid? senderId = null);
        Task<bool> CreatePrescriptionAsync(DoctorPrescriptionModel prescription);
        Task ResolvePrescriptionQueryAsync(Guid prescriptionId, string response);
        Task CancelPrescriptionAsync(Guid prescriptionId, string reason);
        Task<IEnumerable<DoctorPrescriptionModel>> GetRecentlyIssuedPrescriptionsAsync(Guid doctorId);
        Task<IEnumerable<DoctorCalendarSlotModel>> GetCalendarSlotsAsync(Guid doctorId);
        Task<bool> BlockCalendarTimeOffAsync(Guid slotId, string period);
        Task<IEnumerable<WaitingLobbyPatientModel>> GetWaitingPatientsAsync(Guid doctorId);
        Task<ConsultationSessionModel> AcceptPatientConsultationAsync(Guid appointmentId, Guid patientId, Guid doctorId);
        Task<bool> MarkMessagesAsReadAsync(Guid consultationId, bool isDoctorViewer);
        Task<bool> SetConsultationLiveStatusAsync(Guid consultationId, bool isLive);
    }
}
