using QPharmacy.Repositories;
using QPharmacy.Shared.Models.Database;
using QPharmacy.Shared.Service;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QPharmacy.Services
{
    public class DoctorService : IDoctorService
    {
        private readonly IDoctorRepository _repository;
        private readonly IAccessGuard _accessGuard;
        private readonly IPrescriptionWorkflowService _prescriptionWorkflow;

        public DoctorService(IDoctorRepository repository, IAccessGuard accessGuard, IPrescriptionWorkflowService prescriptionWorkflow)
        {
            _repository = repository;
            _accessGuard = accessGuard;
            _prescriptionWorkflow = prescriptionWorkflow;
        }

        public Task<DoctorKpiModel> GetDoctorKpisAsync(Guid doctorId)
        {
            return _repository.GetDoctorKpisAsync(doctorId);
        }

        public Task<IEnumerable<DoctorScheduleItemModel>> GetTodaysScheduleAsync(Guid doctorId)
        {
            return _repository.GetTodaysScheduleAsync(doctorId);
        }

        public Task<IEnumerable<DoctorPendingActionModel>> GetPendingActionsAsync(Guid doctorId)
        {
            return _repository.GetPendingActionsAsync(doctorId);
        }

        public Task<IEnumerable<DoctorPatientModel>> GetDoctorPatientsAsync(Guid doctorId, string? searchQuery = null)
        {
            return _repository.GetDoctorPatientsAsync(doctorId, searchQuery);
        }

        public async Task<bool> AddPatientAsync(DoctorPatientModel patient)
        {
            await _accessGuard.RequireRoleAsync("Doctor");
            return await _repository.AddPatientAsync(patient);
        }

        public Task<ConsultationSessionModel> GetConsultationSessionAsync(Guid patientId)
        {
            return _repository.GetConsultationSessionAsync(patientId);
        }

        public async Task<bool> SaveClinicalNoteAndReferralAsync(Guid consultationId, string assessment, string plan, bool createReferral)
        {
            await _accessGuard.RequireRoleAsync("Doctor");
            return await _repository.SaveClinicalNoteAndReferralAsync(consultationId, assessment, plan, createReferral);
        }

        public async Task<bool> CompleteConsultationAsync(Guid consultationId, string assessment, string plan, string? referralNote, DoctorPrescriptionModel? script = null)
        {
            await _accessGuard.RequireRoleAsync("Doctor");
            if (script != null)
            {
                await CreatePrescriptionAsync(script);
            }
            return await _repository.CompleteConsultationAsync(consultationId, assessment, plan, referralNote, script);
        }

        public Task<ConsultationSessionModel?> GetConsultationSessionByIdAsync(Guid consultationId)
        {
            return _repository.GetConsultationSessionByIdAsync(consultationId);
        }

        public Task<ConsultationSessionModel?> GetConsultationSessionByAppointmentIdAsync(Guid appointmentId)
        {
            return _repository.GetConsultationSessionByAppointmentIdAsync(appointmentId);
        }

        public async Task<bool> SendChatMessageAsync(Guid consultationId, string senderName, string text, bool isDoctor, Guid? senderId = null)
        {
            await _accessGuard.RequireAnyRoleAsync("Doctor", "Patient", "Admin", "Super Admin");
            return await _repository.SendChatMessageAsync(consultationId, senderName, text, isDoctor, senderId);
        }

        public async Task<bool> CreatePrescriptionAsync(DoctorPrescriptionModel prescription)
        {
            await _accessGuard.RequireRoleAsync("Doctor");
            var workflowPrescription = new WorkflowPrescriptionModel
            {
                Id = prescription.Id,
                PatientId = prescription.PatientId,
                PatientName = prescription.PatientName,
                PreferredPharmacy = prescription.PreferredPharmacy,
                Items =
                [
                    new WorkflowPrescriptionItemModel
                    {
                        MedicationName = prescription.Medication,
                        Dosage = prescription.DosageDuration,
                        Frequency = prescription.DosageDuration,
                        DurationDays = 30,
                        Quantity = 1
                    }
                ]
            };
            await _prescriptionWorkflow.IssueAsync(workflowPrescription);
            prescription.Id = workflowPrescription.Id;
            prescription.Status = "Awaiting verification";
            return true;
        }

        public async Task<IEnumerable<DoctorPrescriptionModel>> GetRecentlyIssuedPrescriptionsAsync(Guid doctorId)
        {
            var canonical = await _prescriptionWorkflow.GetForDoctorAsync(doctorId);
            var mapped = canonical.SelectMany(prescription => prescription.Items.Select(item => new DoctorPrescriptionModel
            {
                Id = prescription.Id,
                PatientId = prescription.PatientId,
                PatientName = prescription.PatientName,
                PreferredPharmacy = prescription.PreferredPharmacy,
                Medication = item.MedicationName,
                DosageDuration = item.Dosage,
                Status = prescription.Status.ToString(),
                CreatedAt = prescription.IssuedAt
            }));
            return mapped.OrderByDescending(item => item.CreatedAt);
        }

        public Task ResolvePrescriptionQueryAsync(Guid prescriptionId, string response) =>
            _prescriptionWorkflow.ResolveQueryAsync(prescriptionId, response);

        public Task CancelPrescriptionAsync(Guid prescriptionId, string reason) =>
            _prescriptionWorkflow.CancelAsync(prescriptionId, reason);

        public Task<IEnumerable<DoctorCalendarSlotModel>> GetCalendarSlotsAsync(Guid doctorId)
        {
            return _repository.GetCalendarSlotsAsync(doctorId);
        }

        public async Task<bool> BlockCalendarTimeOffAsync(Guid slotId, string period)
        {
            await _accessGuard.RequireRoleAsync("Doctor");
            return await _repository.BlockCalendarTimeOffAsync(slotId, period);
        }

        public Task<IEnumerable<WaitingLobbyPatientModel>> GetWaitingPatientsAsync(Guid doctorId)
        {
            return _repository.GetWaitingPatientsAsync(doctorId);
        }

        public async Task<ConsultationSessionModel> AcceptPatientConsultationAsync(Guid appointmentId, Guid patientId, Guid doctorId)
        {
            await _accessGuard.RequireRoleAsync("Doctor");
            return await _repository.AcceptPatientConsultationAsync(appointmentId, patientId, doctorId);
        }

        public Task<bool> MarkMessagesAsReadAsync(Guid consultationId, bool isDoctorViewer)
        {
            return _repository.MarkMessagesAsReadAsync(consultationId, isDoctorViewer);
        }

        public Task<bool> SetConsultationLiveStatusAsync(Guid consultationId, bool isLive)
        {
            return _repository.SetConsultationLiveStatusAsync(consultationId, isLive);
        }
    }
}
