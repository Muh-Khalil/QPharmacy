-- ==============================================================================
-- Tables for Patient Features (Dashboard & Prescriptions)
-- ==============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Prescriptions')
BEGIN
    CREATE TABLE Prescriptions (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PatientId UNIQUEIDENTIFIER NOT NULL, -- FK to Users
        MedicationName NVARCHAR(255) NOT NULL,
        Dosage NVARCHAR(100) NOT NULL,
        PrescribedBy NVARCHAR(255) NOT NULL,
        PharmacyName NVARCHAR(255) NOT NULL,
        Status NVARCHAR(50) NOT NULL, -- Verified, Awaiting pharmacist, Uploaded
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MedicationSchedules')
BEGIN
    CREATE TABLE MedicationSchedules (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PatientId UNIQUEIDENTIFIER NOT NULL, -- FK to Users
        MedicationName NVARCHAR(255) NOT NULL,
        ScheduledTime DATETIME2 NOT NULL,
        Status NVARCHAR(50) NOT NULL -- Taken, Upcoming, Missed
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ActivityLogs')
BEGIN
    CREATE TABLE ActivityLogs (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PatientId UNIQUEIDENTIFIER NOT NULL, -- FK to Users
        Description NVARCHAR(500) NOT NULL,
        EventType NVARCHAR(100) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Appointments')
BEGIN
    CREATE TABLE Appointments (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PatientId UNIQUEIDENTIFIER NOT NULL, -- FK to Users
        DoctorName NVARCHAR(255) NOT NULL,
        Specialty NVARCHAR(100) NOT NULL,
        AppointmentDate DATETIME2 NOT NULL,
        Status NVARCHAR(50) NOT NULL -- Scheduled, Completed, Cancelled
    );
END
GO

-- ==============================================================================
-- Stored Procedures
-- ==============================================================================

-- GET PRESCRIPTIONS
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetPatientPrescriptions')
    DROP PROCEDURE sp_GetPatientPrescriptions;
GO

CREATE PROCEDURE sp_GetPatientPrescriptions
    @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        Id,
        PatientId,
        MedicationName,
        Dosage,
        PrescribedBy,
        PharmacyName,
        Status,
        CreatedAt
    FROM Prescriptions
    WHERE PatientId = @PatientId
    ORDER BY CreatedAt DESC;
END
GO

-- GET MEDICATION SCHEDULE (For Today)
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetMedicationScheduleToday')
    DROP PROCEDURE sp_GetMedicationScheduleToday;
GO

CREATE PROCEDURE sp_GetMedicationScheduleToday
    @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TodayStart DATETIME2 = CAST(CAST(GETUTCDATE() AS DATE) AS DATETIME2);
    DECLARE @TodayEnd DATETIME2 = DATEADD(day, 1, @TodayStart);
    
    SELECT 
        Id,
        PatientId,
        MedicationName,
        ScheduledTime,
        Status
    FROM MedicationSchedules
    WHERE PatientId = @PatientId
      AND ScheduledTime >= @TodayStart
      AND ScheduledTime < @TodayEnd
    ORDER BY ScheduledTime ASC;
END
GO

-- GET ACTIVITY LOG (Recent 10)
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetPatientActivityLog')
    DROP PROCEDURE sp_GetPatientActivityLog;
GO

CREATE PROCEDURE sp_GetPatientActivityLog
    @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT TOP 10
        Id,
        PatientId,
        Description,
        EventType,
        CreatedAt
    FROM ActivityLogs
    WHERE PatientId = @PatientId
    ORDER BY CreatedAt DESC;
END
GO

-- GET NEXT APPOINTMENT
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetNextAppointment')
    DROP PROCEDURE sp_GetNextAppointment;
GO

CREATE PROCEDURE sp_GetNextAppointment
    @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT TOP 1
        Id,
        PatientId,
        DoctorName,
        Specialty,
        AppointmentDate,
        Status
    FROM Appointments
    WHERE PatientId = @PatientId
      AND AppointmentDate >= GETUTCDATE()
      AND Status = 'Scheduled'
    ORDER BY AppointmentDate ASC;
END
GO

-- GET DASHBOARD KPIs
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetDashboardKpis')
    DROP PROCEDURE sp_GetDashboardKpis;
GO

CREATE PROCEDURE sp_GetDashboardKpis
    @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TodayStart DATETIME2 = CAST(CAST(GETUTCDATE() AS DATE) AS DATETIME2);
    DECLARE @TodayEnd DATETIME2 = DATEADD(day, 1, @TodayStart);
    
    DECLARE @DosesScheduledToday INT = (SELECT COUNT(*) FROM MedicationSchedules WHERE PatientId = @PatientId AND ScheduledTime >= @TodayStart AND ScheduledTime < @TodayEnd);
    DECLARE @DosesTakenToday INT = (SELECT COUNT(*) FROM MedicationSchedules WHERE PatientId = @PatientId AND ScheduledTime >= @TodayStart AND ScheduledTime < @TodayEnd AND Status = 'Taken');
    DECLARE @ActivePrescriptions INT = (SELECT COUNT(*) FROM Prescriptions WHERE PatientId = @PatientId AND Status IN ('Verified', 'Awaiting pharmacist'));
    
    -- Mocking Family Members and Next Refill for now as they require more complex domain logic
    DECLARE @NextRefillDays INT = 12; 
    DECLARE @FamilyMembersLinked INT = 2;
    
    SELECT 
        @DosesTakenToday AS DosesTakenToday,
        @DosesScheduledToday AS DosesScheduledToday,
        @NextRefillDays AS NextRefillDays,
        @ActivePrescriptions AS ActivePrescriptions,
        @FamilyMembersLinked AS FamilyMembersLinked;
END
GO

-- SEED MOCK DATA (Optional - For Development)
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_SeedPatientMockData')
    DROP PROCEDURE sp_SeedPatientMockData;
GO

CREATE PROCEDURE sp_SeedPatientMockData
    @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Only seed if empty
    IF NOT EXISTS (SELECT 1 FROM Prescriptions WHERE PatientId = @PatientId)
    BEGIN
        INSERT INTO Prescriptions (PatientId, MedicationName, Dosage, PrescribedBy, PharmacyName, Status) VALUES
        (@PatientId, 'Amlodipine 5mg', 'Once daily - 30 tablets', 'Dr. N. Adams', 'Clicks, Claremont', 'Verified'),
        (@PatientId, 'Metformin 500mg', 'Twice daily - 60 tablets', 'Dr. N. Adams', 'Clicks, Claremont', 'Verified'),
        (@PatientId, 'Atorvastatin 20mg', 'Nightly - 30 tablets', 'Dr. N. Adams', 'Clicks, Claremont', 'Awaiting pharmacist'),
        (@PatientId, 'Cetirizine 10mg', 'As needed - 20 tablets', 'Self-uploaded', 'Dis-Chem, Tokai', 'Uploaded');
    END
    
    IF NOT EXISTS (SELECT 1 FROM MedicationSchedules WHERE PatientId = @PatientId)
    BEGIN
        DECLARE @TodayStart DATETIME2 = CAST(CAST(GETUTCDATE() AS DATE) AS DATETIME2);
        
        INSERT INTO MedicationSchedules (PatientId, MedicationName, ScheduledTime, Status) VALUES
        (@PatientId, 'Amlodipine 5mg', DATEADD(hour, 8, @TodayStart), 'Taken'),
        (@PatientId, 'Metformin 500mg', DATEADD(hour, 8, @TodayStart), 'Taken'),
        (@PatientId, 'Metformin 500mg', DATEADD(hour, 20, @TodayStart), 'Upcoming'),
        (@PatientId, 'Atorvastatin 20mg', DATEADD(hour, 21, @TodayStart), 'Upcoming');
    END
    
    IF NOT EXISTS (SELECT 1 FROM Appointments WHERE PatientId = @PatientId)
    BEGIN
        INSERT INTO Appointments (PatientId, DoctorName, Specialty, AppointmentDate, Status) VALUES
        (@PatientId, 'Dr. Sarah Jenkins', 'Cardiologist', DATEADD(day, 3, GETUTCDATE()), 'Scheduled');
    END
    
    IF NOT EXISTS (SELECT 1 FROM ActivityLogs WHERE PatientId = @PatientId)
    BEGIN
        INSERT INTO ActivityLogs (PatientId, Description, EventType, CreatedAt) VALUES
        (@PatientId, 'Prescription for Atorvastatin 20mg uploaded', 'Upload', DATEADD(hour, -2, GETUTCDATE())),
        (@PatientId, 'Medication order #1042 dispatched', 'Order', DATEADD(hour, -24, GETUTCDATE()));
    END
END
GO
