using System;

namespace QPharmacy.Shared.Models.Database
{
    public class AdminKpiModel
    {
        public int ActivePatientsCount { get; set; } = 18420;
        public int PartnerPharmaciesCount { get; set; } = 212;
        public int DoctorsCount { get; set; } = 64;
        public decimal PlatformRevenueMtd { get; set; } = 2400000.00m;

        public string FormattedRevenue => $"R {(PlatformRevenueMtd / 1000000m):0.0}m";
    }

    public class OnboardingApplicantModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Category { get; set; } = "Pharmacies"; // Pharmacies, Doctors, Drivers
        public string Name { get; set; } = string.Empty;
        public string SubText { get; set; } = string.Empty;
        public string Location { get; set; } = "Western Cape";
        public string SubmittedDate { get; set; } = DateTime.UtcNow.ToString("d MMM yyyy");
        public string Status { get; set; } = "Pending review"; // Pending review, Approved
        public string VettingNotes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AdminUserModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "Patient"; // Patient, Doctor, Pharmacist, Driver, Super Admin
        public string LastActive { get; set; } = "Today";
        public string Status { get; set; } = "Active"; // Active, Deactivated
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AdminAuditLogModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        public string EventType { get; set; } = string.Empty;
        public string Category { get; set; } = "Security"; // Security, Integrations, System
        public string Actor { get; set; } = "Admin User";
        public string Details { get; set; } = string.Empty;
        public string Status { get; set; } = "Success"; // Success, Warning, Failed
        public string OriginIp { get; set; } = "192.168.1.104";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AdminCmsModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = "Health Guide"; // Health Guide, Promotional Banner, Platform Announcement
        public string PublishedDate { get; set; } = DateTime.UtcNow.ToString("d MMM yyyy");
        public string Status { get; set; } = "Published"; // Published, Draft
        public string ContentBody { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
