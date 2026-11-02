namespace QPharmacy.Shared.Models.Database;

public sealed class FulfilmentItemModel
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid PrescriptionId { get; set; }
    public Guid PrescriptionItemId { get; set; }
    public string RequestedMedication { get; set; } = string.Empty;
    public string DispensedMedication { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public int FulfilledQuantity { get; set; }
    public bool IsSubstitution { get; set; }
    public string SubstitutionReason { get; set; } = string.Empty;
    public string Status { get; set; } = "Reserved";
}
