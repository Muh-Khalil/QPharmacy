using System;

namespace QPharmacy.Shared.Models.Database
{
    public class OrderModel
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public Guid PatientId { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty; // 'Dispensed', 'In transit', 'Delivered'
        public int ItemsCount { get; set; }
        public string DispatchedFrom { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = "Unpaid"; // 'Unpaid', 'Paid', 'PartiallyPaid'
    }

    public class MarketplaceProductModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string IconName { get; set; } = string.Empty; // e.g. ShoppingBag, Activity, Package
        public string Category { get; set; } = "General";
        public string Description { get; set; } = string.Empty;
        public bool InStock { get; set; } = true;
        public decimal Rating { get; set; } = 4.8m;
        public string? Badge { get; set; }
    }
}
