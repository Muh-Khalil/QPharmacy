using System;

namespace QPharmacy.Shared.Models.Database
{
    public class PrescriptionModel
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public string Dosage { get; set; } = string.Empty;
        public string PrescribedBy { get; set; } = string.Empty;
        public string PharmacyName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Verified, Awaiting pharmacist, Uploaded
        public DateTime CreatedAt { get; set; }
        public DateTime? LastDispensedDate { get; set; }
        public int SupplyDays { get; set; } = 30;
        public int RepeatsRemaining { get; set; } = 2;

        // Dynamic Calculations based on Last Dispensed Date
        public DateTime NextRefillDueDate => LastDispensedDate.HasValue 
            ? LastDispensedDate.Value.AddDays(SupplyDays) 
            : (CreatedAt != default ? CreatedAt.AddDays(SupplyDays - 5) : DateTime.Now.AddDays(5));

        public int DaysUntilRefill => (int)Math.Max(0, Math.Ceiling((NextRefillDueDate - DateTime.Now).TotalDays));

        public bool IsRefillAvailable => RepeatsRemaining > 0 && DaysUntilRefill <= 7;
    }

    public class MedicationScheduleModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PatientId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public string Dosage { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public DateTime ScheduledTime { get; set; }
        public string Status { get; set; } = "Upcoming"; // Taken, Upcoming, Skipped, Missed
        public DateTime? TakenAt { get; set; }
        public string? Notes { get; set; }
    }

    public class ActivityLogModel
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public string Description { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class AppointmentModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PatientId { get; set; }
        public Guid? DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public string Status { get; set; } = "Scheduled"; // Scheduled, InProgress, Completed, Cancelled
        public string Mode { get; set; } = "Video"; // Video, InPerson
        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class DashboardKpiModel
    {
        public int DosesTakenToday { get; set; }
        public int DosesScheduledToday { get; set; }
        public int NextRefillDays { get; set; }
        public int ActivePrescriptions { get; set; }
        public int FamilyMembersLinked { get; set; }
        public int Adherence30Days { get; set; } = 94; // 30-day rolled-up adherence percentage
        
        // Calculated Property
        public int AdherencePercentage => DosesScheduledToday == 0 ? 100 : (int)Math.Round((double)DosesTakenToday / DosesScheduledToday * 100);
    }
}
