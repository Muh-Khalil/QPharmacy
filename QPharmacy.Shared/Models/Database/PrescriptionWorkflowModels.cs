namespace QPharmacy.Shared.Models.Database;

public enum PrescriptionWorkflowStatus
{
    Draft,
    AwaitingVerification,
    Queried,
    Approved,
    Rejected,
    Cancelled
}

public sealed class WorkflowPrescriptionModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PrescriptionNumber { get; set; } = string.Empty;
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public string PreferredPharmacy { get; set; } = string.Empty;
    public PrescriptionWorkflowStatus Status { get; set; } = PrescriptionWorkflowStatus.Draft;
    public DateTime IssuedAt { get; set; }
    public DateTime? SignedAt { get; set; }
    public string SignatureHash { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
    public List<WorkflowPrescriptionItemModel> Items { get; set; } = [];
}

public sealed class WorkflowPrescriptionItemModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PrescriptionId { get; set; }
    public string MedicationName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public int Quantity { get; set; }
    public int RepeatsAllowed { get; set; }
    public int RepeatsRemaining { get; set; }
}

public sealed class PrescriptionReviewModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PrescriptionId { get; set; }
    public Guid ActorUserId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class PrescriptionStatusHistoryModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PrescriptionId { get; set; }
    public PrescriptionWorkflowStatus FromStatus { get; set; }
    public PrescriptionWorkflowStatus ToStatus { get; set; }
    public Guid ChangedByUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
