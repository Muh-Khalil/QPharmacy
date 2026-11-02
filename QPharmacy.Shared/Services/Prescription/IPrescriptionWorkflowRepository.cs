using QPharmacy.Shared.Models.Database;

namespace QPharmacy.Repositories;

public interface IPrescriptionWorkflowRepository
{
    Task<Guid> IssueAsync(WorkflowPrescriptionModel prescription);
    Task<WorkflowPrescriptionModel?> GetAsync(Guid prescriptionId);
    Task<IEnumerable<WorkflowPrescriptionModel>> GetForPatientAsync(Guid patientId);
    Task<IEnumerable<WorkflowPrescriptionModel>> GetForDoctorAsync(Guid doctorId);
    Task<IEnumerable<WorkflowPrescriptionModel>> GetVerificationQueueAsync(string pharmacyBranch);
    Task<bool> TransitionAsync(
        Guid prescriptionId,
        PrescriptionWorkflowStatus expectedStatus,
        PrescriptionWorkflowStatus newStatus,
        Guid actorUserId,
        string reason);
    Task<Guid> CreateRefillAsync(Guid prescriptionId, Guid patientId, Guid actorUserId);
}
