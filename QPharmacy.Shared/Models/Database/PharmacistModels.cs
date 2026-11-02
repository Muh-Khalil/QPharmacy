using System;

namespace QPharmacy.Shared.Models.Database
{
    public class PharmacyInventoryModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string BranchName { get; set; } = "Clicks, Claremont";
        public string MedicationName { get; set; } = string.Empty;
        public int InStock { get; set; }
        public int ReorderLevel { get; set; }
        public string Status { get; set; } = "Healthy"; // Healthy, Low, Reorder now
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class VerificationQueueModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string PrescriptionNumber { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string MedicationDetails { get; set; } = string.Empty;
        public string InteractionStatus { get; set; } = string.Empty;
        public bool IsFlagged { get; set; }
        public string Initials { get; set; } = string.Empty;
        public string AvatarBgColor { get; set; } = "bg-teal-600";
        public string MedicationShort { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, Approved, Queried
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class DispensingOrderModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string OrderNumber { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public string Fulfilment { get; set; } = "Delivery"; // Delivery, Click & collect
        public string HandoverRef { get; set; } = string.Empty;
        public string ActionLabel { get; set; } = "Mark packed"; // Mark packed, Mark ready, Dispatched, Patient Notified
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string PaymentStatus { get; set; } = "Unpaid"; // 'Unpaid', 'Paid'
    }

    public class PharmacistKpiModel
    {
        public int AwaitingVerificationCount { get; set; }
        public int ReadyForCollectionCount { get; set; }
        public int LowStockCount { get; set; }
        public int DispensedTodayCount { get; set; }
    }
}
