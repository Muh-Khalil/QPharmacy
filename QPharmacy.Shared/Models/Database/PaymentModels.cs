using System;

namespace QPharmacy.Shared.Models.Database
{
    public class MedicalAidModel
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public string SchemeName { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public string MemberNumber { get; set; } = string.Empty;
        public decimal AnnualLimit { get; set; }
        public decimal AmountUsed { get; set; }
        public string Status { get; set; } = string.Empty; // 'Active', 'Suspended', 'Expired'
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class ClaimModel
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty; // 'Approved', 'Pending', 'Rejected'
        public DateTime ClaimDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PaymentMethodModel
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public string CardType { get; set; } = string.Empty; // 'Visa', 'Mastercard', etc.
        public string LastFour { get; set; } = string.Empty;
        public string ExpiryDate { get; set; } = string.Empty; // 'MM/YY'
        public string GatewayToken { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PaymentIntentModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OrderId { get; set; }
        public Guid PatientId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "ZAR";
        public string PaymentMethod { get; set; } = "MedicalAid"; // 'MedicalAid', 'Card', 'Split'
        public string Status { get; set; } = "Pending"; // 'Pending', 'Succeeded', 'Failed'
        public string? GatewayTransactionId { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class PaymentAllocationModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PaymentIntentId { get; set; }
        public Guid OrderId { get; set; }
        public string Source { get; set; } = "MedicalAid"; // 'MedicalAid', 'Card'
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Approved"; // 'Approved', 'Settled', 'Failed'
        public Guid? ClaimId { get; set; }
        public Guid? PaymentMethodId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class WebhookEventModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string EventId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public string Status { get; set; } = "Processed";
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }

    public class ProcessPaymentRequest
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public Guid PatientId { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentType { get; set; } = "MedicalAid"; // 'MedicalAid', 'Card', 'Split'
        public decimal MedicalAidAmount { get; set; }
        public decimal CardAmount { get; set; }
        public Guid? CardPaymentMethodId { get; set; }
        public string? IdempotencyKey { get; set; }
    }

    public class ProcessPaymentResult
    {
        public bool Success { get; set; }
        public Guid? PaymentIntentId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public decimal RemainingMedicalAidBenefit { get; set; }
    }
}
