using QPharmacy.Repositories;
using QPharmacy.Shared.Models.Database;
using QPharmacy.Shared.Service;
using System.Security.Cryptography;
using System.Text;

namespace QPharmacy.Services;

public sealed class PrescriptionWorkflowService : IPrescriptionWorkflowService
{
    private readonly IPrescriptionWorkflowRepository _repository;
    private readonly IAccessGuard _accessGuard;
    private readonly ICurrentUserContext _currentUser;

    public PrescriptionWorkflowService(
        IPrescriptionWorkflowRepository repository,
        IAccessGuard accessGuard,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _accessGuard = accessGuard;
        _currentUser = currentUser;
    }

    public async Task<Guid> IssueAsync(WorkflowPrescriptionModel prescription)
    {
        await _accessGuard.RequireRoleAsync("Doctor");
        ValidatePrescription(prescription);
        prescription.DoctorId = _currentUser.UserId;
        prescription.DoctorName = _currentUser.DisplayName;
        prescription.Status = PrescriptionWorkflowStatus.AwaitingVerification;
        prescription.IssuedAt = DateTime.UtcNow;
        prescription.SignedAt = prescription.IssuedAt;
        prescription.SignatureHash = CreateSignatureHash(prescription);
        prescription.UpdatedAt = prescription.IssuedAt;
        prescription.Version = 1;
        prescription.PrescriptionNumber = string.IsNullOrWhiteSpace(prescription.PrescriptionNumber)
            ? $"RX-{DateTime.UtcNow:yyyyMMdd}-{prescription.Id.ToString("N")[..6].ToUpperInvariant()}"
            : prescription.PrescriptionNumber;
        return await _repository.IssueAsync(prescription);
    }

    public async Task<IEnumerable<WorkflowPrescriptionModel>> GetForPatientAsync(Guid patientId)
    {
        await _accessGuard.RequireSelfAsync(patientId);
        return await _repository.GetForPatientAsync(patientId);
    }

    public async Task<Guid> UploadAsync(WorkflowPrescriptionModel prescription)
    {
        await _accessGuard.RequireSelfAsync(prescription.PatientId);
        ValidatePrescription(prescription);
        prescription.DoctorId = Guid.Empty;
        prescription.Status = PrescriptionWorkflowStatus.AwaitingVerification;
        prescription.IssuedAt = DateTime.UtcNow;
        prescription.UpdatedAt = prescription.IssuedAt;
        prescription.Version = 1;
        prescription.PrescriptionNumber = $"UP-{DateTime.UtcNow:yyyyMMdd}-{prescription.Id.ToString("N")[..6].ToUpperInvariant()}";
        return await _repository.IssueAsync(prescription);
    }

    public async Task<IEnumerable<WorkflowPrescriptionModel>> GetVerificationQueueAsync()
    {
        await _accessGuard.RequireRoleAsync("Pharmacist");
        if (string.IsNullOrWhiteSpace(_currentUser.OrganizationOrBranch))
            throw new UnauthorizedAccessException("The pharmacist account is not assigned to a pharmacy branch.");
        return await _repository.GetVerificationQueueAsync(_currentUser.OrganizationOrBranch);
    }

    public async Task<IEnumerable<WorkflowPrescriptionModel>> GetForDoctorAsync(Guid doctorId)
    {
        await _accessGuard.RequireRoleAsync("Doctor");
        if (doctorId != _currentUser.UserId)
            throw new UnauthorizedAccessException("Doctors can only access their own prescriptions.");
        return await _repository.GetForDoctorAsync(doctorId);
    }

    public async Task QueryDoctorAsync(Guid prescriptionId, string note)
    {
        await _accessGuard.RequireRoleAsync("Pharmacist");
        if (string.IsNullOrWhiteSpace(note)) throw new ArgumentException("A query note is required.", nameof(note));
        await TransitionAsync(prescriptionId, PrescriptionWorkflowStatus.AwaitingVerification, PrescriptionWorkflowStatus.Queried, note);
    }

    public async Task ResolveQueryAsync(Guid prescriptionId, string response)
    {
        await _accessGuard.RequireRoleAsync("Doctor");
        var prescription = await GetRequiredAsync(prescriptionId);
        if (prescription.DoctorId != _currentUser.UserId) throw new UnauthorizedAccessException("Only the issuing doctor can resolve this query.");
        await TransitionAsync(prescriptionId, PrescriptionWorkflowStatus.Queried, PrescriptionWorkflowStatus.AwaitingVerification, response);
    }

    public async Task ApproveAsync(Guid prescriptionId)
    {
        await _accessGuard.RequireRoleAsync("Pharmacist");
        await TransitionAsync(prescriptionId, PrescriptionWorkflowStatus.AwaitingVerification, PrescriptionWorkflowStatus.Approved, "Approved by pharmacist");
    }

    public async Task RejectAsync(Guid prescriptionId, string reason)
    {
        await _accessGuard.RequireRoleAsync("Pharmacist");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A rejection reason is required.", nameof(reason));
        await TransitionAsync(prescriptionId, PrescriptionWorkflowStatus.AwaitingVerification, PrescriptionWorkflowStatus.Rejected, reason);
    }

    public async Task CancelAsync(Guid prescriptionId, string reason)
    {
        await _accessGuard.RequireRoleAsync("Doctor");
        var prescription = await GetRequiredAsync(prescriptionId);
        if (prescription.DoctorId != _currentUser.UserId)
            throw new UnauthorizedAccessException("Only the issuing doctor can cancel this prescription.");
        if (prescription.Status is not (PrescriptionWorkflowStatus.AwaitingVerification or PrescriptionWorkflowStatus.Queried))
            throw new InvalidOperationException("Only unverified prescriptions can be cancelled.");
        await TransitionAsync(prescriptionId, prescription.Status, PrescriptionWorkflowStatus.Cancelled,
            string.IsNullOrWhiteSpace(reason) ? "Cancelled by issuing doctor" : reason);
    }

    public async Task<Guid> RequestRefillAsync(Guid prescriptionId, Guid patientId)
    {
        await _accessGuard.RequireSelfAsync(patientId);
        return await _repository.CreateRefillAsync(prescriptionId, patientId, _currentUser.UserId);
    }

    private async Task TransitionAsync(Guid id, PrescriptionWorkflowStatus expected, PrescriptionWorkflowStatus next, string reason)
    {
        var changed = await _repository.TransitionAsync(id, expected, next, _currentUser.UserId, reason.Trim());
        if (!changed) throw new InvalidOperationException($"Prescription is not in the expected '{expected}' state.");
    }

    private async Task<WorkflowPrescriptionModel> GetRequiredAsync(Guid id) =>
        await _repository.GetAsync(id) ?? throw new KeyNotFoundException("Prescription was not found.");

    private static void ValidatePrescription(WorkflowPrescriptionModel prescription)
    {
        if (prescription.PatientId == Guid.Empty) throw new ArgumentException("A patient is required.");
        if (string.IsNullOrWhiteSpace(prescription.PatientName)) throw new ArgumentException("Patient name is required.");
        if (prescription.Items.Count == 0) throw new ArgumentException("At least one medication is required.");
        if (prescription.Items.Any(item => string.IsNullOrWhiteSpace(item.MedicationName) || item.Quantity <= 0))
            throw new ArgumentException("Every medication requires a name and positive quantity.");
    }

    private static string CreateSignatureHash(WorkflowPrescriptionModel prescription)
    {
        var payload = string.Join('|', prescription.Id, prescription.PatientId, prescription.DoctorId,
            prescription.IssuedAt.ToString("O"),
            string.Join(';', prescription.Items.Select(item => $"{item.MedicationName}:{item.Dosage}:{item.Quantity}")));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
