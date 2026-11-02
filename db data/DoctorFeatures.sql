-- ---------------------------------------------------------------------
-- DOCTOR MODULE TABLES AND STORED PROCEDURES
-- ---------------------------------------------------------------------

-- 1. DoctorPatients Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DoctorPatients')
BEGIN
    CREATE TABLE DoctorPatients (
        PatientId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        DoctorId UNIQUEIDENTIFIER NULL,
        PatientName NVARCHAR(255) NOT NULL,
        Age INT NOT NULL,
        LastVisitDate NVARCHAR(100) NOT NULL,
        ConditionSummary NVARCHAR(500) NOT NULL,
        MedicalAid NVARCHAR(200) NOT NULL,
        Initials NVARCHAR(10) NULL,
        Email NVARCHAR(255) NULL,
        Phone NVARCHAR(50) NULL,
        CreatedAt DATETIME DEFAULT GETUTCDATE()
    );

    INSERT INTO DoctorPatients (PatientId, PatientName, Age, LastVisitDate, ConditionSummary, MedicalAid, Initials, Email, Phone) VALUES
    ('A1B2C3D4-E5F6-7A8B-9C0D-1E2F3A4B5C6D', 'Thandiwe Mokoena', 41, '2 Jul 2026', 'Hypertension, T2D', 'Discovery Health', 'TM', 'thandiwe@example.com', '082 123 4567'),
    ('B2C3D4E5-F6A7-8B9C-0D1E-2F3A4B5C6D7E', 'Jabu Khumalo', 34, '29 Jun 2026', 'New patient', 'Bonitas', 'JK', 'jabu@example.com', '083 234 5678'),
    ('C3D4E5F6-A7B8-9C0D-1E2F-3A4B5C6D7E8F', 'Rose Pillay', 58, '18 Jun 2026', 'Chronic, cardiac', 'Discovery Health', 'RP', 'rose@example.com', '084 345 6789');
END;
GO

-- 2. DoctorSchedules Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DoctorSchedules')
BEGIN
    CREATE TABLE DoctorSchedules (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        DoctorId UNIQUEIDENTIFIER NULL,
        PatientId UNIQUEIDENTIFIER NOT NULL,
        PatientName NVARCHAR(255) NOT NULL,
        Initials NVARCHAR(10) NULL,
        AvatarBgColor NVARCHAR(50) DEFAULT 'bg-emerald-600',
        Time NVARCHAR(20) NOT NULL,
        AppointmentType NVARCHAR(100) NOT NULL,
        Mode NVARCHAR(50) DEFAULT 'Video',
        Status NVARCHAR(50) DEFAULT 'Scheduled',
        AppointmentDate DATETIME DEFAULT GETUTCDATE()
    );

    INSERT INTO DoctorSchedules (Id, PatientId, PatientName, Initials, AvatarBgColor, Time, AppointmentType, Mode, Status) VALUES
    ('11111111-1111-1111-1111-111111111111', 'A1B2C3D4-E5F6-7A8B-9C0D-1E2F3A4B5C6D', 'Thandiwe Mokoena', 'TM', 'bg-emerald-600', '09:30', 'Follow-up', 'Video', 'Scheduled'),
    ('22222222-2222-2222-2222-222222222222', 'B2C3D4E5-F6A7-8B9C-0D1E-2F3A4B5C6D7E', 'Jabu Khumalo', 'JK', 'bg-blue-600', '10:15', 'New patient', 'Video', 'Scheduled'),
    ('33333333-3333-3333-3333-333333333333', 'C3D4E5F6-A7B8-9C0D-1E2F-3A4B5C6D7E8F', 'Rose Pillay', 'RP', 'bg-amber-500', '11:00', 'Chronic review', 'Video', 'Scheduled');
END;
GO

-- 3. DoctorPendingActions Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DoctorPendingActions')
BEGIN
    CREATE TABLE DoctorPendingActions (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        DoctorId UNIQUEIDENTIFIER NULL,
        ActionType NVARCHAR(50) NOT NULL,
        Title NVARCHAR(255) NOT NULL,
        Subtitle NVARCHAR(255) NOT NULL,
        Count INT DEFAULT 1,
        IconType NVARCHAR(50) DEFAULT 'Warning',
        CreatedAt DATETIME DEFAULT GETUTCDATE()
    );

    INSERT INTO DoctorPendingActions (Id, ActionType, Title, Subtitle, Count, IconType) VALUES
    (NEWID(), 'Referral', '2 referrals awaiting sign-off', 'Specialist referrals', 2, 'Warning'),
    (NEWID(), 'Interaction', '1 prescription flagged for interaction', 'Requires review', 1, 'File');
END;
GO

-- 4. DoctorConsultations Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DoctorConsultations')
BEGIN
    CREATE TABLE DoctorConsultations (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PatientId UNIQUEIDENTIFIER NOT NULL,
        DoctorId UNIQUEIDENTIFIER NULL,
        PatientName NVARCHAR(255) NOT NULL,
        CallTimerDisplay NVARCHAR(20) DEFAULT '14:22',
        IsLive BIT DEFAULT 1,
        Assessment NVARCHAR(MAX) NULL,
        [Plan] NVARCHAR(MAX) NULL,
        CreatedAt DATETIME DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME NULL
    );

    INSERT INTO DoctorConsultations (Id, PatientId, PatientName, CallTimerDisplay, IsLive, Assessment, [Plan]) VALUES
    (NEWID(), 'A1B2C3D4-E5F6-7A8B-9C0D-1E2F3A4B5C6D', 'Thandiwe Mokoena', '14:22', 1, 
     'Blood pressure well controlled. HbA1c improved to 6.8%. Continue current regimen.', 
     'Repeat bloods in 4 weeks. Issue digital referral to dietitian.');
END;
GO

-- 5. DoctorConsultationMessages Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DoctorConsultationMessages')
BEGIN
    CREATE TABLE DoctorConsultationMessages (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        ConsultationId UNIQUEIDENTIFIER NOT NULL,
        SenderName NVARCHAR(255) NOT NULL,
        IsDoctor BIT DEFAULT 1,
        MessageText NVARCHAR(MAX) NOT NULL,
        Timestamp DATETIME DEFAULT GETUTCDATE()
    );
END;
GO

-- 6. DoctorPrescriptions Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DoctorPrescriptions')
BEGIN
    CREATE TABLE DoctorPrescriptions (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        DoctorId UNIQUEIDENTIFIER NULL,
        PatientId UNIQUEIDENTIFIER NOT NULL,
        PatientName NVARCHAR(255) NOT NULL,
        PreferredPharmacy NVARCHAR(255) NOT NULL,
        Medication NVARCHAR(255) NOT NULL,
        DosageDuration NVARCHAR(255) NOT NULL,
        Status NVARCHAR(50) DEFAULT 'Awaiting verification',
        CreatedAt DATETIME DEFAULT GETUTCDATE()
    );

    INSERT INTO DoctorPrescriptions (Id, PatientId, PatientName, PreferredPharmacy, Medication, DosageDuration, Status, CreatedAt) VALUES
    (NEWID(), 'A1B2C3D4-E5F6-7A8B-9C0D-1E2F3A4B5C6D', 'Thandiwe Mokoena', 'Clicks, Claremont', 'Atorvastatin 20mg', 'Nightly, 30 days', 'Awaiting verification', DATEADD(HOUR, -2, GETDATE())),
    (NEWID(), 'C3D4E5F6-A7B8-9C0D-1E2F-3A4B5C6D7E8F', 'Rose Pillay', 'Dis-Chem, Tokai', 'Bisoprolol 5mg', 'Once daily, 30 days', 'Verified', DATEADD(DAY, -1, GETDATE()));
END;
GO

-- 7. DoctorCalendarSlots Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DoctorCalendarSlots')
BEGIN
    CREATE TABLE DoctorCalendarSlots (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        DoctorId UNIQUEIDENTIFIER NULL,
        DayLabel NVARCHAR(50) NOT NULL,
        MorningStatus NVARCHAR(50) NOT NULL,
        AfternoonStatus NVARCHAR(50) NOT NULL,
        IsMorningBlocked BIT DEFAULT 0,
        IsAfternoonBlocked BIT DEFAULT 0,
        CreatedAt DATETIME DEFAULT GETUTCDATE()
    );

    INSERT INTO DoctorCalendarSlots (Id, DayLabel, MorningStatus, AfternoonStatus, IsMorningBlocked, IsAfternoonBlocked) VALUES
    (NEWID(), 'Mon 6 Jul', '4 booked', '2 booked', 0, 0),
    (NEWID(), 'Tue 7 Jul', '3 booked', 'Open', 0, 0),
    (NEWID(), 'Wed 8 Jul', 'Open', '5 booked', 0, 0);
END;
GO

-- ---------------------------------------------------------------------
-- STORED PROCEDURES
-- ---------------------------------------------------------------------

CREATE OR ALTER PROCEDURE sp_GetDoctorKpis
    @DoctorId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @TodaysAppointments INT;
    DECLARE @PendingReferrals INT;
    DECLARE @PrescriptionsWeek INT;

    SELECT @TodaysAppointments = COUNT(*) FROM DoctorSchedules WHERE (@DoctorId IS NULL OR DoctorId = @DoctorId) AND CAST(AppointmentDate AS DATE) = CAST(GETDATE() AS DATE);
    IF @TodaysAppointments = 0
        SELECT @TodaysAppointments = COUNT(*) FROM DoctorSchedules WHERE (@DoctorId IS NULL OR DoctorId = @DoctorId);

    SELECT @PendingReferrals = ISNULL(SUM(Count), 0) FROM DoctorPendingActions WHERE (@DoctorId IS NULL OR DoctorId = @DoctorId) AND ActionType = 'Referral';
    SELECT @PrescriptionsWeek = COUNT(*) FROM DoctorPrescriptions WHERE (@DoctorId IS NULL OR DoctorId = @DoctorId) AND CreatedAt >= DATEADD(DAY, -7, GETDATE());

    SELECT 
        ISNULL(@TodaysAppointments, 6) AS TodaysAppointments,
        ISNULL(@PendingReferrals, 2) AS PendingReferrals,
        ISNULL(@PrescriptionsWeek, 31) AS PrescriptionsIssuedWeek,
        14 AS AvgConsultationLengthMinutes;
END;
GO

CREATE OR ALTER PROCEDURE sp_GetDoctorSchedule
    @DoctorId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, PatientId, PatientName, Initials, AvatarBgColor, Time, AppointmentType, Mode, Status 
    FROM DoctorSchedules 
    WHERE (@DoctorId IS NULL OR DoctorId = @DoctorId)
    ORDER BY Time ASC;
END;
GO

CREATE OR ALTER PROCEDURE sp_GetDoctorPendingActions
    @DoctorId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, ActionType, Title, Subtitle, Count, IconType 
    FROM DoctorPendingActions 
    WHERE (@DoctorId IS NULL OR DoctorId = @DoctorId);
END;
GO

CREATE OR ALTER PROCEDURE sp_GetDoctorPatients
    @DoctorId UNIQUEIDENTIFIER = NULL,
    @SearchQuery NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PatientId, PatientName, Age, LastVisitDate, ConditionSummary, MedicalAid, Initials, Email, Phone 
    FROM DoctorPatients 
    WHERE (@DoctorId IS NULL OR DoctorId = @DoctorId)
      AND (@SearchQuery IS NULL OR @SearchQuery = '' OR PatientName LIKE '%' + @SearchQuery + '%' OR ConditionSummary LIKE '%' + @SearchQuery + '%')
    ORDER BY CreatedAt DESC;
END;
GO

CREATE OR ALTER PROCEDURE sp_AddDoctorPatient
    @PatientId UNIQUEIDENTIFIER,
    @DoctorId UNIQUEIDENTIFIER = NULL,
    @PatientName NVARCHAR(255),
    @Age INT,
    @LastVisitDate NVARCHAR(100),
    @ConditionSummary NVARCHAR(500),
    @MedicalAid NVARCHAR(200),
    @Initials NVARCHAR(10) = NULL,
    @Email NVARCHAR(255) = NULL,
    @Phone NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @PatientId IS NULL OR @PatientId = '00000000-0000-0000-0000-000000000000'
        SET @PatientId = NEWID();

    INSERT INTO DoctorPatients (PatientId, DoctorId, PatientName, Age, LastVisitDate, ConditionSummary, MedicalAid, Initials, Email, Phone)
    VALUES (@PatientId, @DoctorId, @PatientName, @Age, @LastVisitDate, @ConditionSummary, @MedicalAid, @Initials, @Email, @Phone);

    SELECT 1;
END;
GO
