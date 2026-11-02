using System;

namespace QPharmacy.Shared.Models.Database
{
    public class FamilyMemberModel
    {
        public Guid Id { get; set; }
        public Guid PrimaryPatientId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty; // e.g. Self, Child, Parent
        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
    }

    public class MedicalConditionModel
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // e.g. Chronic, Allergy
        public string Notes { get; set; } = string.Empty; // e.g. Diagnosed 2022 - Managed
        public DateTime RecordedAt { get; set; }
    }

    public class LabResultModel
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public string TestName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty; // e.g. 6.8%, 128/82, Normal
        public string Status { get; set; } = string.Empty; // e.g. Normal, Attention
        public DateTime TestDate { get; set; }
    }

    public class VaccinationModel
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public string VaccineName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // e.g. Up to date, Due
        public string Notes { get; set; } = string.Empty; // e.g. April 2026, Due 2026
        public DateTime? DateAdministered { get; set; }
        public DateTime? DateDue { get; set; }
    }
}
