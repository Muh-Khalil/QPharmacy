using System;
using System.Collections.Generic;

namespace QPharmacy.Shared.Models.Database
{
    public class DoctorKpiModel
    {
        public int TodaysAppointments { get; set; } = 6;
        public int PendingReferrals { get; set; } = 2;
        public int PrescriptionsIssuedWeek { get; set; } = 31;
        public int AvgConsultationLengthMinutes { get; set; } = 14;
    }

    public class DoctorScheduleItemModel
    {
        public Guid Id { get; set; }
        public Guid? AppointmentId { get; set; }
        public Guid PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string AvatarBgColor { get; set; } = "bg-emerald-600";
        public string Time { get; set; } = string.Empty; // e.g. "09:30"
        public string AppointmentType { get; set; } = string.Empty; // e.g. "Follow-up"
        public string Mode { get; set; } = "Video"; // e.g. "Video"
        public string Status { get; set; } = "Scheduled";
        public DateTime? AppointmentDate { get; set; }
    }

    public class WaitingLobbyPatientModel
    {
        public Guid AppointmentId { get; set; }
        public Guid PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string AvatarBgColor { get; set; } = "bg-teal-600";
        public string AppointmentType { get; set; } = string.Empty;
        public DateTime JoinedLobbyAt { get; set; } = DateTime.UtcNow;
        public string ScheduledTime { get; set; } = string.Empty;
        public int WaitingMinutes => Math.Max(0, (int)(DateTime.UtcNow - JoinedLobbyAt).TotalMinutes);
    }

    public class DoctorPendingActionModel
    {
        public Guid Id { get; set; }
        public string ActionType { get; set; } = string.Empty; // "Referral", "Interaction"
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public int Count { get; set; }
        public string IconType { get; set; } = "Warning";
    }

    public class DoctorPatientModel
    {
        public Guid PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public int Age { get; set; }
        public string LastVisitDate { get; set; } = string.Empty; // e.g. "2 Jul 2026"
        public string ConditionSummary { get; set; } = string.Empty; // e.g. "Hypertension, T2D"
        public string MedicalAid { get; set; } = string.Empty; // e.g. "Discovery Health"
        public string Initials { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }

    public class ConsultationChatMessageModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ConsultationId { get; set; }
        public Guid? SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public bool IsDoctor { get; set; }
        public string MessageText { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;
    }

    public class ConsultationSessionModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? AppointmentId { get; set; }
        public Guid PatientId { get; set; }
        public Guid? DoctorId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public string CallTimerDisplay { get; set; } = "14:22";
        public bool IsLive { get; set; } = true;
        public string Status { get; set; } = "InProgress";
        public string Assessment { get; set; } = string.Empty;
        public string Plan { get; set; } = string.Empty;
        public string ReferralNote { get; set; } = string.Empty;
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
        public List<ConsultationChatMessageModel> ChatMessages { get; set; } = new();
    }

    public class DoctorPrescriptionModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PreferredPharmacy { get; set; } = string.Empty;
        public string Medication { get; set; } = string.Empty;
        public string DosageDuration { get; set; } = string.Empty;
        public string Status { get; set; } = "Awaiting verification"; // "Awaiting verification", "Verified", "Queried"
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class DoctorCalendarSlotModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DayLabel { get; set; } = string.Empty; // e.g. "Mon 6 Jul"
        public string MorningStatus { get; set; } = "Open"; // "4 booked", "Open", "Blocked"
        public string AfternoonStatus { get; set; } = "Open"; // "2 booked", "Open", "Blocked"
        public bool IsMorningBlocked { get; set; }
        public bool IsAfternoonBlocked { get; set; }
    }
}
