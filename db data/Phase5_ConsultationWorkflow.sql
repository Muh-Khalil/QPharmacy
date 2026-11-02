-- ==========================================================
-- Phase 5: Consultations, Appointments & Telehealth Workflow
-- ==========================================================

USE QPharmacy;
GO

-- 1. Ensure Columns on dbo.Appointments
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'DoctorId')
BEGIN
    ALTER TABLE dbo.Appointments ADD DoctorId UNIQUEIDENTIFIER NULL;
END
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'Mode')
BEGIN
    ALTER TABLE dbo.Appointments ADD Mode NVARCHAR(50) NOT NULL CONSTRAINT DF_Appointments_Mode DEFAULT 'Video';
END
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'Notes')
BEGIN
    ALTER TABLE dbo.Appointments ADD Notes NVARCHAR(MAX) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'CreatedAt')
BEGIN
    ALTER TABLE dbo.Appointments ADD CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Appointments_CreatedAt DEFAULT SYSUTCDATETIME();
END
GO

-- 2. Ensure Columns on dbo.DoctorConsultations
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DoctorConsultations' AND COLUMN_NAME = 'AppointmentId')
BEGIN
    ALTER TABLE dbo.DoctorConsultations ADD AppointmentId UNIQUEIDENTIFIER NULL;
END
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DoctorConsultations' AND COLUMN_NAME = 'DoctorName')
BEGIN
    ALTER TABLE dbo.DoctorConsultations ADD DoctorName NVARCHAR(255) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DoctorConsultations' AND COLUMN_NAME = 'Status')
BEGIN
    ALTER TABLE dbo.DoctorConsultations ADD Status NVARCHAR(50) NOT NULL CONSTRAINT DF_DoctorConsultations_Status DEFAULT 'InProgress';
END
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DoctorConsultations' AND COLUMN_NAME = 'ReferralNote')
BEGIN
    ALTER TABLE dbo.DoctorConsultations ADD ReferralNote NVARCHAR(MAX) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DoctorConsultations' AND COLUMN_NAME = 'StartedAt')
BEGIN
    ALTER TABLE dbo.DoctorConsultations ADD StartedAt DATETIME NULL;
END
GO

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DoctorConsultations' AND COLUMN_NAME = 'EndedAt')
BEGIN
    ALTER TABLE dbo.DoctorConsultations ADD EndedAt DATETIME NULL;
END
GO

-- 3. Ensure Columns on dbo.DoctorSchedules
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DoctorSchedules' AND COLUMN_NAME = 'AppointmentId')
BEGIN
    ALTER TABLE dbo.DoctorSchedules ADD AppointmentId UNIQUEIDENTIFIER NULL;
END
GO

-- 4. Seed a default active Doctor Patient link if missing
IF NOT EXISTS (SELECT 1 FROM dbo.DoctorPatients WHERE PatientName = 'Thandiwe Mokoena')
BEGIN
    INSERT INTO dbo.DoctorPatients (PatientId, PatientName, Age, LastVisitDate, ConditionSummary, MedicalAid, Initials, Email, Phone)
    VALUES ('a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d', 'Thandiwe Mokoena', 34, '19 Sep 2026', 'Hypertension management, Routine follow-up', 'Discovery Health (Classic Saver)', 'TM', 'thandiwe@example.com', '+27 82 555 0192');
END
GO

PRINT 'Phase 5 database schema update completed successfully.';
