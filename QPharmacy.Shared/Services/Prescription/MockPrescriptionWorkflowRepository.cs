using QPharmacy.Shared.Models.Database;

namespace QPharmacy.Repositories;

public sealed class MockPrescriptionWorkflowRepository : IPrescriptionWorkflowRepository
{
    private readonly object _sync = new();
    private readonly List<WorkflowPrescriptionModel> _prescriptions = [];
    private readonly List<PrescriptionStatusHistoryModel> _history = [];

    public Task<Guid> IssueAsync(WorkflowPrescriptionModel prescription)
    {
        lock (_sync)
        {
            foreach (var item in prescription.Items)
                item.PrescriptionId = prescription.Id;
            _prescriptions.Add(prescription);
            _history.Add(new PrescriptionStatusHistoryModel
            {
                PrescriptionId = prescription.Id,
                FromStatus = PrescriptionWorkflowStatus.Draft,
                ToStatus = prescription.Status,
                ChangedByUserId = prescription.DoctorId,
                Reason = "Prescription issued"
            });
            return Task.FromResult(prescription.Id);
        }
    }

    public Task<WorkflowPrescriptionModel?> GetAsync(Guid prescriptionId)
    {
        lock (_sync) return Task.FromResult(_prescriptions.SingleOrDefault(x => x.Id == prescriptionId));
    }

    public Task<IEnumerable<WorkflowPrescriptionModel>> GetForDoctorAsync(Guid doctorId)
    {
        lock (_sync)
            return Task.FromResult<IEnumerable<WorkflowPrescriptionModel>>(_prescriptions.Where(item => item.DoctorId == doctorId).ToList());
    }

    public Task<IEnumerable<WorkflowPrescriptionModel>> GetForPatientAsync(Guid patientId)
    {
        lock (_sync) return Task.FromResult<IEnumerable<WorkflowPrescriptionModel>>(_prescriptions.Where(x => x.PatientId == patientId).OrderByDescending(x => x.IssuedAt).ToList());
    }

    public Task<IEnumerable<WorkflowPrescriptionModel>> GetVerificationQueueAsync(string pharmacyBranch)
    {
        lock (_sync) return Task.FromResult<IEnumerable<WorkflowPrescriptionModel>>(_prescriptions.Where(x =>
            string.Equals(x.PreferredPharmacy, pharmacyBranch, StringComparison.OrdinalIgnoreCase) &&
            (x.Status is PrescriptionWorkflowStatus.AwaitingVerification or PrescriptionWorkflowStatus.Queried)).OrderBy(x => x.IssuedAt).ToList());
    }

    public Task<bool> TransitionAsync(Guid prescriptionId, PrescriptionWorkflowStatus expectedStatus, PrescriptionWorkflowStatus newStatus, Guid actorUserId, string reason)
    {
        lock (_sync)
        {
            var prescription = _prescriptions.SingleOrDefault(x => x.Id == prescriptionId);
            if (prescription is null || prescription.Status != expectedStatus) return Task.FromResult(false);
            prescription.Status = newStatus;
            prescription.UpdatedAt = DateTime.UtcNow;
            prescription.Version++;
            _history.Add(new PrescriptionStatusHistoryModel { PrescriptionId = prescriptionId, FromStatus = expectedStatus, ToStatus = newStatus, ChangedByUserId = actorUserId, Reason = reason });
            return Task.FromResult(true);
        }
    }

    public Task<Guid> CreateRefillAsync(Guid prescriptionId, Guid patientId, Guid actorUserId) =>
        throw new NotSupportedException("Refill fulfilment requires the SQL repository.");
}
