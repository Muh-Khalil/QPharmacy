using QPharmacy.Shared.Models.Database;

namespace QPharmacy.Services;

public interface IPrescriptionWorkflowService
{
    Task<Guid> IssueAsync(WorkflowPrescriptionModel prescription);
    Task<Guid> UploadAsync(WorkflowPrescriptionModel prescription);
    Task<IEnumerable<WorkflowPrescriptionModel>> GetForPatientAsync(Guid patientId);
    Task<IEnumerable<WorkflowPrescriptionModel>> GetForDoctorAsync(Guid doctorId);
    Task<IEnumerable<WorkflowPrescriptionModel>> GetVerificationQueueAsync();
    Task QueryDoctorAsync(Guid prescriptionId, string note);
    Task ResolveQueryAsync(Guid prescriptionId, string response);
    Task ApproveAsync(Guid prescriptionId);
    Task RejectAsync(Guid prescriptionId, string reason);
    Task CancelAsync(Guid prescriptionId, string reason);
    Task<Guid> RequestRefillAsync(Guid prescriptionId, Guid patientId);
}
