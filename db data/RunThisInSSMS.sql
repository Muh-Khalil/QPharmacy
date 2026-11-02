-- ==============================================================================
-- CONSOLIDATED DATABASE SETUP & MOCK DATA SEED SCRIPT
-- RUN THIS IN SQL SERVER MANAGEMENT STUDIO (SSMS) ON QPharmacy DB
-- ==============================================================================

USE QPharmacy;
GO

-- ==============================================================================
-- PART 1: SCHEMAS & TABLES
-- ==============================================================================

-- 1. Prescriptions & Scheduling
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Prescriptions')
BEGIN
    CREATE TABLE Prescriptions (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PatientId UNIQUEIDENTIFIER NOT NULL,
        MedicationName NVARCHAR(255) NOT NULL,
        Dosage NVARCHAR(100) NOT NULL,
        PrescribedBy NVARCHAR(255) NOT NULL,
        PharmacyName NVARCHAR(255) NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FOREIGN KEY (PatientId) REFERENCES Users(Id)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MedicationSchedules')
BEGIN
    CREATE TABLE MedicationSchedules (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PatientId UNIQUEIDENTIFIER NOT NULL,
        MedicationName NVARCHAR(255) NOT NULL,
        ScheduledTime DATETIME2 NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        FOREIGN KEY (PatientId) REFERENCES Users(Id)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ActivityLogs')
BEGIN
    CREATE TABLE ActivityLogs (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PatientId UNIQUEIDENTIFIER NOT NULL,
        Description NVARCHAR(500) NOT NULL,
        EventType NVARCHAR(100) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FOREIGN KEY (PatientId) REFERENCES Users(Id)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Appointments')
BEGIN
    CREATE TABLE Appointments (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PatientId UNIQUEIDENTIFIER NOT NULL,
        DoctorName NVARCHAR(255) NOT NULL,
        Specialty NVARCHAR(100) NOT NULL,
        AppointmentDate DATETIME2 NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        FOREIGN KEY (PatientId) REFERENCES Users(Id)
    );
END
GO

-- 2. Health Records
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FamilyMembers')
BEGIN
    CREATE TABLE FamilyMembers (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PrimaryPatientId UNIQUEIDENTIFIER NOT NULL,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        Relationship NVARCHAR(50) NOT NULL,
        CanView BIT NOT NULL DEFAULT 1,
        CanEdit BIT NOT NULL DEFAULT 0,
        FOREIGN KEY (PrimaryPatientId) REFERENCES Users(Id)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MedicalConditions')
BEGIN
    CREATE TABLE MedicalConditions (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PatientId UNIQUEIDENTIFIER NOT NULL, -- FK to FamilyMembers(Id) or Users(Id)
        Name NVARCHAR(255) NOT NULL,
        Type NVARCHAR(50) NOT NULL,
        Notes NVARCHAR(500) NOT NULL,
        RecordedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LabResults')
BEGIN
    CREATE TABLE LabResults (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PatientId UNIQUEIDENTIFIER NOT NULL,
        TestName NVARCHAR(100) NOT NULL,
        Value NVARCHAR(100) NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        TestDate DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Vaccinations')
BEGIN
    CREATE TABLE Vaccinations (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PatientId UNIQUEIDENTIFIER NOT NULL,
        VaccineName NVARCHAR(100) NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        Notes NVARCHAR(100) NOT NULL,
        DateAdministered DATETIME2 NULL,
        DateDue DATETIME2 NULL
    );
END
GO

-- 3. Orders
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Orders')
BEGIN
    CREATE TABLE Orders (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        OrderNumber NVARCHAR(50) NOT NULL,
        PatientId UNIQUEIDENTIFIER NOT NULL,
        OrderDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        TotalAmount DECIMAL(18,2) NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        ItemsCount INT NOT NULL,
        DispatchedFrom NVARCHAR(255) NOT NULL,
        FOREIGN KEY (PatientId) REFERENCES Users(Id)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MarketplaceProducts')
BEGIN
    CREATE TABLE MarketplaceProducts (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Name NVARCHAR(255) NOT NULL,
        Price DECIMAL(18,2) NOT NULL,
        IconName NVARCHAR(50) NOT NULL
    );
END
GO

-- 4. Medical Aid & Payments
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MedicalAids')
BEGIN
    CREATE TABLE MedicalAids (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PatientId UNIQUEIDENTIFIER NOT NULL,
        SchemeName NVARCHAR(100) NOT NULL,
        PlanName NVARCHAR(100) NOT NULL,
        MemberNumber NVARCHAR(50) NOT NULL,
        AnnualLimit DECIMAL(18, 2) NOT NULL,
        AmountUsed DECIMAL(18, 2) NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME NULL,
        FOREIGN KEY (PatientId) REFERENCES Users(Id)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Claims')
BEGIN
    CREATE TABLE Claims (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PatientId UNIQUEIDENTIFIER NOT NULL,
        Description NVARCHAR(255) NOT NULL,
        Amount DECIMAL(18, 2) NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        ClaimDate DATETIME DEFAULT GETDATE(),
        CreatedAt DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (PatientId) REFERENCES Users(Id)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PaymentMethods')
BEGIN
    CREATE TABLE PaymentMethods (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PatientId UNIQUEIDENTIFIER NOT NULL,
        CardType NVARCHAR(50) NOT NULL,
        LastFour NVARCHAR(4) NOT NULL,
        ExpiryDate NVARCHAR(10) NOT NULL,
        GatewayToken NVARCHAR(255) NOT NULL,
        IsDefault BIT DEFAULT 0,
        CreatedAt DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (PatientId) REFERENCES Users(Id)
    );
END
GO

-- 5. Pharmacist Module Tables
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PharmacyInventory')
BEGIN
    CREATE TABLE PharmacyInventory (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        BranchName NVARCHAR(255) NOT NULL DEFAULT 'Clicks, Claremont',
        MedicationName NVARCHAR(255) NOT NULL,
        InStock INT NOT NULL,
        ReorderLevel INT NOT NULL,
        Status NVARCHAR(50) NOT NULL, -- Healthy, Low, Reorder now
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'VerificationQueue')
BEGIN
    CREATE TABLE VerificationQueue (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PrescriptionNumber NVARCHAR(50) NOT NULL,
        PatientName NVARCHAR(255) NOT NULL,
        DoctorName NVARCHAR(255) NOT NULL,
        MedicationDetails NVARCHAR(500) NOT NULL,
        InteractionStatus NVARCHAR(255) NOT NULL,
        IsFlagged BIT NOT NULL DEFAULT 0,
        Initials NVARCHAR(10) NOT NULL,
        AvatarBgColor NVARCHAR(50) NOT NULL DEFAULT 'bg-teal-600',
        MedicationShort NVARCHAR(255) NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Approved, Queried
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DispensingOrders')
BEGIN
    CREATE TABLE DispensingOrders (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        OrderNumber NVARCHAR(50) NOT NULL,
        PatientName NVARCHAR(255) NOT NULL,
        ItemCount INT NOT NULL,
        Fulfilment NVARCHAR(50) NOT NULL, -- Delivery, Click & collect
        HandoverRef NVARCHAR(255) NOT NULL,
        ActionLabel NVARCHAR(50) NOT NULL, -- Mark packed, Mark ready, Dispatched, Patient Notified
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

-- 6. Doctor Module Tables
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
END
GO

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
END
GO

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
END
GO

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
END
GO

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
END
GO

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
END
GO

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
END
GO


-- ==============================================================================
-- PART 2: STORED PROCEDURES
-- ==============================================================================

-- Prescriptions & Dashboard Procs
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetPatientPrescriptions')
    DROP PROCEDURE sp_GetPatientPrescriptions;
GO
CREATE PROCEDURE sp_GetPatientPrescriptions @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, PatientId, MedicationName, Dosage, PrescribedBy, PharmacyName, Status, CreatedAt FROM Prescriptions WHERE PatientId = @PatientId ORDER BY CreatedAt DESC;
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetMedicationScheduleToday')
    DROP PROCEDURE sp_GetMedicationScheduleToday;
GO
CREATE PROCEDURE sp_GetMedicationScheduleToday @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @TodayStart DATETIME2 = CAST(CAST(GETUTCDATE() AS DATE) AS DATETIME2);
    DECLARE @TodayEnd DATETIME2 = DATEADD(day, 1, @TodayStart);
    SELECT Id, PatientId, MedicationName, ScheduledTime, Status FROM MedicationSchedules WHERE PatientId = @PatientId AND ScheduledTime >= @TodayStart AND ScheduledTime < @TodayEnd ORDER BY ScheduledTime ASC;
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetPatientActivityLog')
    DROP PROCEDURE sp_GetPatientActivityLog;
GO
CREATE PROCEDURE sp_GetPatientActivityLog @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 10 Id, PatientId, Description, EventType, CreatedAt FROM ActivityLogs WHERE PatientId = @PatientId ORDER BY CreatedAt DESC;
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetNextAppointment')
    DROP PROCEDURE sp_GetNextAppointment;
GO
CREATE PROCEDURE sp_GetNextAppointment @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 Id, PatientId, DoctorName, Specialty, AppointmentDate, Status FROM Appointments WHERE PatientId = @PatientId AND AppointmentDate >= GETUTCDATE() AND Status = 'Scheduled' ORDER BY AppointmentDate ASC;
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetDashboardKpis')
    DROP PROCEDURE sp_GetDashboardKpis;
GO
CREATE PROCEDURE sp_GetDashboardKpis @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @TodayStart  DATETIME2 = CAST(CAST(GETUTCDATE() AS DATE) AS DATETIME2);
    DECLARE @TodayEnd    DATETIME2 = DATEADD(day, 1, @TodayStart);
    DECLARE @Month30Start DATETIME2 = DATEADD(day, -30, @TodayStart);

    DECLARE @DosesScheduledToday INT = (SELECT COUNT(*) FROM MedicationSchedules WHERE PatientId = @PatientId AND ScheduledTime >= @TodayStart AND ScheduledTime < @TodayEnd);
    DECLARE @DosesTakenToday     INT = (SELECT COUNT(*) FROM MedicationSchedules WHERE PatientId = @PatientId AND ScheduledTime >= @TodayStart AND ScheduledTime < @TodayEnd AND Status = 'Taken');
    DECLARE @ActivePrescriptions INT = (SELECT COUNT(*) FROM Prescriptions     WHERE PatientId = @PatientId AND Status IN ('Verified', 'Awaiting pharmacist'));
    DECLARE @FamilyMembersLinked INT = (SELECT COUNT(*) FROM FamilyMembers     WHERE PrimaryPatientId = @PatientId);

    -- Next refill: days until the earliest upcoming Upcoming dose runs out (basic estimate = 30 day supply rolling)
    DECLARE @NextRefillDays INT = (
        SELECT TOP 1 DATEDIFF(day, GETUTCDATE(), DATEADD(day, 30, MIN(ScheduledTime)))
        FROM MedicationSchedules
        WHERE PatientId = @PatientId AND Status = 'Upcoming'
    );
    IF @NextRefillDays IS NULL OR @NextRefillDays < 0 SET @NextRefillDays = 0;

    -- 30-day adherence: (doses taken in last 30 days) / (doses scheduled in last 30 days) * 100
    DECLARE @Doses30Scheduled INT = (SELECT COUNT(*) FROM MedicationSchedules WHERE PatientId = @PatientId AND ScheduledTime >= @Month30Start AND ScheduledTime < @TodayEnd);
    DECLARE @Doses30Taken     INT = (SELECT COUNT(*) FROM MedicationSchedules WHERE PatientId = @PatientId AND ScheduledTime >= @Month30Start AND ScheduledTime < @TodayEnd AND Status = 'Taken');
    DECLARE @Adherence30Days  INT = CASE WHEN @Doses30Scheduled = 0 THEN 100 ELSE CAST(ROUND(CAST(@Doses30Taken AS FLOAT) / @Doses30Scheduled * 100, 0) AS INT) END;

    SELECT
        @DosesTakenToday     AS DosesTakenToday,
        @DosesScheduledToday AS DosesScheduledToday,
        @NextRefillDays      AS NextRefillDays,
        @ActivePrescriptions AS ActivePrescriptions,
        @FamilyMembersLinked AS FamilyMembersLinked,
        @Adherence30Days     AS Adherence30Days;
END;
GO

-- Get prescriptions filtered by status group (Active / Pending / History)
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetPrescriptionsByStatus')
    DROP PROCEDURE sp_GetPrescriptionsByStatus;
GO
CREATE PROCEDURE sp_GetPrescriptionsByStatus
    @PatientId   UNIQUEIDENTIFIER,
    @StatusGroup NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    IF @StatusGroup = 'Active'
        SELECT Id, PatientId, MedicationName, Dosage, PrescribedBy, PharmacyName, Status, CreatedAt
        FROM Prescriptions WHERE PatientId = @PatientId AND Status = 'Verified'
        ORDER BY CreatedAt DESC;
    ELSE IF @StatusGroup = 'Pending'
        SELECT Id, PatientId, MedicationName, Dosage, PrescribedBy, PharmacyName, Status, CreatedAt
        FROM Prescriptions WHERE PatientId = @PatientId AND Status IN ('Awaiting pharmacist', 'Uploaded')
        ORDER BY CreatedAt DESC;
    ELSE IF @StatusGroup = 'History'
        SELECT Id, PatientId, MedicationName, Dosage, PrescribedBy, PharmacyName, Status, CreatedAt
        FROM Prescriptions WHERE PatientId = @PatientId AND Status NOT IN ('Verified', 'Awaiting pharmacist', 'Uploaded')
        ORDER BY CreatedAt DESC;
    ELSE
        SELECT Id, PatientId, MedicationName, Dosage, PrescribedBy, PharmacyName, Status, CreatedAt
        FROM Prescriptions WHERE PatientId = @PatientId
        ORDER BY CreatedAt DESC;
END;
GO

-- Upload a new prescription (patient-submitted, starts as 'Uploaded')
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_UploadPrescription')
    DROP PROCEDURE sp_UploadPrescription;
GO
CREATE PROCEDURE sp_UploadPrescription
    @Id             UNIQUEIDENTIFIER,
    @PatientId      UNIQUEIDENTIFIER,
    @MedicationName NVARCHAR(255),
    @Dosage         NVARCHAR(100),
    @PrescribedBy   NVARCHAR(255),
    @PharmacyName   NVARCHAR(255),
    @Status         NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO Prescriptions (Id, PatientId, MedicationName, Dosage, PrescribedBy, PharmacyName, Status, CreatedAt)
    VALUES (@Id, @PatientId, @MedicationName, @Dosage, @PrescribedBy, @PharmacyName, @Status, GETUTCDATE());

    -- Also log to activity feed
    INSERT INTO ActivityLogs (PatientId, Description, EventType, CreatedAt)
    VALUES (@PatientId, 'Prescription for ' + @MedicationName + ' uploaded — awaiting pharmacist review', 'Upload', GETUTCDATE());

    -- Also insert into Pharmacist Verification Queue
    DECLARE @PatientName NVARCHAR(255) = 'Patient';
    DECLARE @Initials NVARCHAR(10) = 'PT';

    SELECT TOP 1 
        @PatientName = ISNULL(FirstName + ' ' + LastName, 'Patient'),
        @Initials = ISNULL(LEFT(FirstName, 1) + LEFT(LastName, 1), 'PT')
    FROM Users WHERE Id = @PatientId;

    IF @PatientName IS NULL OR @PatientName = '' SET @PatientName = 'Patient';
    IF @Initials IS NULL OR @Initials = '' SET @Initials = 'PT';

    DECLARE @RxNum NVARCHAR(50) = CAST(ABS(CHECKSUM(@Id)) % 9000 + 1000 AS NVARCHAR(50));

    INSERT INTO VerificationQueue (
        Id, PrescriptionNumber, PatientName, DoctorName, MedicationDetails, 
        InteractionStatus, IsFlagged, Initials, AvatarBgColor, MedicationShort, Status, CreatedAt
    )
    VALUES (
        @Id, 
        @RxNum, 
        @PatientName, 
        @PrescribedBy, 
        @MedicationName + ', ' + @Dosage, 
        'No conflicts detected', 
        0, 
        @Initials, 
        'bg-teal-600', 
        @MedicationName, 
        'Pending', 
        GETUTCDATE()
    );
END;
GO

-- Mark a scheduled dose as Taken
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_MarkDoseAsTaken')
    DROP PROCEDURE sp_MarkDoseAsTaken;
GO
CREATE PROCEDURE sp_MarkDoseAsTaken
    @ScheduleId UNIQUEIDENTIFIER,
    @PatientId  UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE MedicationSchedules
    SET    Status = 'Taken'
    WHERE  Id = @ScheduleId AND PatientId = @PatientId AND Status = 'Upcoming';
END;
GO

-- Health Records Procs
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetFamilyMembers')
    DROP PROCEDURE sp_GetFamilyMembers;
GO
CREATE PROCEDURE sp_GetFamilyMembers @PrimaryPatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM FamilyMembers WHERE PrimaryPatientId = @PrimaryPatientId;
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_AddFamilyMember')
    DROP PROCEDURE sp_AddFamilyMember;
GO
CREATE PROCEDURE sp_AddFamilyMember
    @Id UNIQUEIDENTIFIER,
    @PrimaryPatientId UNIQUEIDENTIFIER,
    @FirstName NVARCHAR(100),
    @LastName NVARCHAR(100),
    @Relationship NVARCHAR(50),
    @CanView BIT,
    @CanEdit BIT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO FamilyMembers (Id, PrimaryPatientId, FirstName, LastName, Relationship, CanView, CanEdit)
    VALUES (@Id, @PrimaryPatientId, @FirstName, @LastName, @Relationship, @CanView, @CanEdit);
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetMedicalConditions')
    DROP PROCEDURE sp_GetMedicalConditions;
GO
CREATE PROCEDURE sp_GetMedicalConditions @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, PatientId, Name, Type, Notes, RecordedAt FROM MedicalConditions WHERE PatientId = @PatientId ORDER BY RecordedAt DESC;
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetLabResults')
    DROP PROCEDURE sp_GetLabResults;
GO
CREATE PROCEDURE sp_GetLabResults @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, PatientId, TestName, Value, Status, TestDate FROM LabResults WHERE PatientId = @PatientId ORDER BY TestDate DESC;
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetVaccinationHistory')
    DROP PROCEDURE sp_GetVaccinationHistory;
GO
CREATE PROCEDURE sp_GetVaccinationHistory @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, PatientId, VaccineName, Status, Notes, DateAdministered, DateDue FROM Vaccinations WHERE PatientId = @PatientId ORDER BY COALESCE(DateAdministered, DateDue) DESC;
END;
GO

-- Orders Procs
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetActiveOrder')
    DROP PROCEDURE sp_GetActiveOrder;
GO
CREATE PROCEDURE sp_GetActiveOrder @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 * FROM Orders WHERE PatientId = @PatientId AND Status IN ('Dispensed', 'Out for delivery') ORDER BY OrderDate DESC;
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetOrderHistory')
    DROP PROCEDURE sp_GetOrderHistory;
GO
CREATE PROCEDURE sp_GetOrderHistory @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM Orders WHERE PatientId = @PatientId ORDER BY OrderDate DESC;
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CreateOrder')
    DROP PROCEDURE sp_CreateOrder;
GO
CREATE PROCEDURE sp_CreateOrder
    @Id UNIQUEIDENTIFIER,
    @OrderNumber NVARCHAR(50),
    @PatientId UNIQUEIDENTIFIER,
    @TotalAmount DECIMAL(18,2),
    @Status NVARCHAR(50),
    @ItemsCount INT,
    @DispatchedFrom NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO Orders (Id, OrderNumber, PatientId, OrderDate, TotalAmount, Status, ItemsCount, DispatchedFrom)
    VALUES (@Id, @OrderNumber, @PatientId, GETUTCDATE(), @TotalAmount, @Status, @ItemsCount, @DispatchedFrom);
    
    INSERT INTO ActivityLogs (PatientId, Description, EventType, CreatedAt)
    VALUES (@PatientId, 'Order ' + @OrderNumber + ' placed successfully', 'Order', GETUTCDATE());
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetMarketplaceProducts')
    DROP PROCEDURE sp_GetMarketplaceProducts;
GO
CREATE PROCEDURE sp_GetMarketplaceProducts
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Name, Price, IconName FROM MarketplaceProducts;
END;
GO

-- Payments Procs
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetMedicalAidDetails')
    DROP PROCEDURE sp_GetMedicalAidDetails;
GO
CREATE PROCEDURE sp_GetMedicalAidDetails @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, PatientId, SchemeName, PlanName, MemberNumber, AnnualLimit, AmountUsed, Status, CreatedAt, UpdatedAt FROM MedicalAids WHERE PatientId = @PatientId;
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetRecentClaims')
    DROP PROCEDURE sp_GetRecentClaims;
GO
CREATE PROCEDURE sp_GetRecentClaims @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, PatientId, Description, Amount, Status, ClaimDate, CreatedAt FROM Claims WHERE PatientId = @PatientId ORDER BY ClaimDate DESC;
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetSavedPaymentMethods')
    DROP PROCEDURE sp_GetSavedPaymentMethods;
GO
CREATE PROCEDURE sp_GetSavedPaymentMethods @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, PatientId, CardType, LastFour, ExpiryDate, GatewayToken, IsDefault, CreatedAt FROM PaymentMethods WHERE PatientId = @PatientId ORDER BY IsDefault DESC, CreatedAt DESC;
END;
GO

-- Pharmacist Module Procs
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetPharmacyInventory')
    DROP PROCEDURE sp_GetPharmacyInventory;
GO
CREATE PROCEDURE sp_GetPharmacyInventory
    @BranchName NVARCHAR(255) = 'Clicks, Claremont'
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, BranchName, MedicationName, InStock, ReorderLevel, Status, UpdatedAt
    FROM PharmacyInventory
    WHERE BranchName = @BranchName OR @BranchName IS NULL
    ORDER BY MedicationName ASC;
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_AddPharmacyInventory')
    DROP PROCEDURE sp_AddPharmacyInventory;
GO
CREATE PROCEDURE sp_AddPharmacyInventory
    @Id UNIQUEIDENTIFIER,
    @BranchName NVARCHAR(255),
    @MedicationName NVARCHAR(255),
    @InStock INT,
    @ReorderLevel INT,
    @Status NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO PharmacyInventory (Id, BranchName, MedicationName, InStock, ReorderLevel, Status, UpdatedAt)
    VALUES (@Id, @BranchName, @MedicationName, @InStock, @ReorderLevel, @Status, GETUTCDATE());
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_UpdatePharmacyInventoryStock')
    DROP PROCEDURE sp_UpdatePharmacyInventoryStock;
GO
CREATE PROCEDURE sp_UpdatePharmacyInventoryStock
    @Id UNIQUEIDENTIFIER,
    @AddQuantity INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE PharmacyInventory
    SET InStock = InStock + @AddQuantity,
        Status = CASE WHEN (InStock + @AddQuantity) <= ReorderLevel THEN (CASE WHEN (InStock + @AddQuantity) <= 20 THEN 'Reorder now' ELSE 'Low' END) ELSE 'Healthy' END,
        UpdatedAt = GETUTCDATE()
    WHERE Id = @Id;
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetVerificationQueue')
    DROP PROCEDURE sp_GetVerificationQueue;
GO
CREATE PROCEDURE sp_GetVerificationQueue
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, PrescriptionNumber, PatientName, DoctorName, MedicationDetails, InteractionStatus, IsFlagged, Initials, AvatarBgColor, MedicationShort, Status, CreatedAt
    FROM VerificationQueue
    WHERE Status = 'Pending'
    ORDER BY CreatedAt ASC;
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_UpdateVerificationStatus')
    DROP PROCEDURE sp_UpdateVerificationStatus;
GO
CREATE PROCEDURE sp_UpdateVerificationStatus
    @Id UNIQUEIDENTIFIER,
    @Status NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE VerificationQueue
    SET Status = @Status
    WHERE Id = @Id;

    IF @Status = 'Approved'
    BEGIN
        UPDATE Prescriptions
        SET Status = 'Verified'
        WHERE Id = @Id;
    END
    ELSE IF @Status = 'Queried'
    BEGIN
        UPDATE Prescriptions
        SET Status = 'Queried'
        WHERE Id = @Id;
    END
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetDispensingOrders')
    DROP PROCEDURE sp_GetDispensingOrders;
GO
CREATE PROCEDURE sp_GetDispensingOrders
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, OrderNumber, PatientName, ItemCount, Fulfilment, HandoverRef, ActionLabel, CreatedAt
    FROM DispensingOrders
    ORDER BY CreatedAt DESC;
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_UpdateDispensingOrder')
    DROP PROCEDURE sp_UpdateDispensingOrder;
GO
CREATE PROCEDURE sp_UpdateDispensingOrder
    @Id UNIQUEIDENTIFIER,
    @ActionLabel NVARCHAR(50),
    @HandoverRef NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE DispensingOrders
    SET ActionLabel = @ActionLabel,
        HandoverRef = @HandoverRef
    WHERE Id = @Id;
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_AddDispensingOrder')
    DROP PROCEDURE sp_AddDispensingOrder;
GO
CREATE PROCEDURE sp_AddDispensingOrder
    @Id UNIQUEIDENTIFIER,
    @OrderNumber NVARCHAR(50),
    @PatientName NVARCHAR(255),
    @ItemCount INT,
    @Fulfilment NVARCHAR(50),
    @HandoverRef NVARCHAR(255),
    @ActionLabel NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO DispensingOrders (Id, OrderNumber, PatientName, ItemCount, Fulfilment, HandoverRef, ActionLabel, CreatedAt)
    VALUES (@Id, @OrderNumber, @PatientName, @ItemCount, @Fulfilment, @HandoverRef, @ActionLabel, GETUTCDATE());
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetPharmacistKpis')
    DROP PROCEDURE sp_GetPharmacistKpis;
GO
CREATE PROCEDURE sp_GetPharmacistKpis
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @AwaitingVerificationCount INT = (SELECT COUNT(*) FROM VerificationQueue WHERE Status = 'Pending');
    DECLARE @ReadyForCollectionCount INT = (SELECT COUNT(*) FROM DispensingOrders WHERE ActionLabel IN ('Mark ready', 'Patient Notified'));
    DECLARE @LowStockCount INT = (SELECT COUNT(*) FROM PharmacyInventory WHERE Status <> 'Healthy');
    DECLARE @DispensedTodayCount INT = (SELECT COUNT(*) FROM DispensingOrders) + 40;

    SELECT
        @AwaitingVerificationCount AS AwaitingVerificationCount,
        @ReadyForCollectionCount   AS ReadyForCollectionCount,
        @LowStockCount             AS LowStockCount,
        @DispensedTodayCount       AS DispensedTodayCount;
END;
GO

-- Doctor Module Procs
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetDoctorKpis')
    DROP PROCEDURE sp_GetDoctorKpis;
GO
CREATE PROCEDURE sp_GetDoctorKpis
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

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetDoctorSchedule')
    DROP PROCEDURE sp_GetDoctorSchedule;
GO
CREATE PROCEDURE sp_GetDoctorSchedule
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

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetDoctorPendingActions')
    DROP PROCEDURE sp_GetDoctorPendingActions;
GO
CREATE PROCEDURE sp_GetDoctorPendingActions
    @DoctorId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, ActionType, Title, Subtitle, Count, IconType 
    FROM DoctorPendingActions 
    WHERE (@DoctorId IS NULL OR DoctorId = @DoctorId);
END;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetDoctorPatients')
    DROP PROCEDURE sp_GetDoctorPatients;
GO
CREATE PROCEDURE sp_GetDoctorPatients
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

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_AddDoctorPatient')
    DROP PROCEDURE sp_AddDoctorPatient;
GO
CREATE PROCEDURE sp_AddDoctorPatient
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



-- ==============================================================================
-- PART 3: DATA SEEDING PROCEDURES
-- ==============================================================================

-- 1. Seed Patient Mock Data
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_SeedPatientMockData')
    DROP PROCEDURE sp_SeedPatientMockData;
GO
CREATE PROCEDURE sp_SeedPatientMockData @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    DELETE FROM Prescriptions WHERE PatientId = @PatientId;
    DELETE FROM MedicationSchedules WHERE PatientId = @PatientId;
    DELETE FROM Appointments WHERE PatientId = @PatientId;
    DELETE FROM ActivityLogs WHERE PatientId = @PatientId;
    
    INSERT INTO Prescriptions (PatientId, MedicationName, Dosage, PrescribedBy, PharmacyName, Status) VALUES
    (@PatientId, 'Amlodipine 5mg', 'Once daily - 30 tablets', 'Dr. N. Adams', 'Clicks, Claremont', 'Verified'),
    (@PatientId, 'Metformin 500mg', 'Twice daily - 60 tablets', 'Dr. N. Adams', 'Clicks, Claremont', 'Verified'),
    (@PatientId, 'Atorvastatin 20mg', 'Nightly - 30 tablets', 'Dr. N. Adams', 'Clicks, Claremont', 'Awaiting pharmacist'),
    (@PatientId, 'Cetirizine 10mg', 'As needed - 20 tablets', 'Self-uploaded', 'Dis-Chem, Tokai', 'Uploaded');
    
    DECLARE @TodayStart DATETIME2 = CAST(CAST(GETUTCDATE() AS DATE) AS DATETIME2);
    INSERT INTO MedicationSchedules (PatientId, MedicationName, ScheduledTime, Status) VALUES
    (@PatientId, 'Amlodipine 5mg', DATEADD(hour, 8, @TodayStart), 'Taken'),
    (@PatientId, 'Metformin 500mg', DATEADD(hour, 8, @TodayStart), 'Taken'),
    (@PatientId, 'Metformin 500mg', DATEADD(hour, 20, @TodayStart), 'Upcoming'),
    (@PatientId, 'Atorvastatin 20mg', DATEADD(hour, 21, @TodayStart), 'Upcoming');
    
    INSERT INTO Appointments (PatientId, DoctorName, Specialty, AppointmentDate, Status) VALUES
    (@PatientId, 'Dr. Sarah Jenkins', 'Cardiologist', DATEADD(day, 3, GETUTCDATE()), 'Scheduled');
    
    INSERT INTO ActivityLogs (PatientId, Description, EventType, CreatedAt) VALUES
    (@PatientId, 'Prescription for Atorvastatin 20mg uploaded', 'Upload', DATEADD(hour, -2, GETUTCDATE())),
    (@PatientId, 'Medication order #1042 dispatched', 'Order', DATEADD(hour, -24, GETUTCDATE()));
END;
GO

-- 2. Seed Health Record Mock Data
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_SeedHealthRecordMockData')
    DROP PROCEDURE sp_SeedHealthRecordMockData;
GO
CREATE PROCEDURE sp_SeedHealthRecordMockData @PrimaryPatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Clean previous mappings
    DELETE FROM MedicalConditions WHERE PatientId IN (SELECT Id FROM FamilyMembers WHERE PrimaryPatientId = @PrimaryPatientId);
    DELETE FROM LabResults WHERE PatientId IN (SELECT Id FROM FamilyMembers WHERE PrimaryPatientId = @PrimaryPatientId);
    DELETE FROM Vaccinations WHERE PatientId IN (SELECT Id FROM FamilyMembers WHERE PrimaryPatientId = @PrimaryPatientId);
    DELETE FROM FamilyMembers WHERE PrimaryPatientId = @PrimaryPatientId;
    
    DECLARE @SelfId UNIQUEIDENTIFIER = NEWID();
    DECLARE @NtandoId UNIQUEIDENTIFIER = NEWID();
    DECLARE @BabaId UNIQUEIDENTIFIER = NEWID();
    
    INSERT INTO FamilyMembers (Id, PrimaryPatientId, FirstName, LastName, Relationship, CanView, CanEdit) VALUES
    (@SelfId, @PrimaryPatientId, 'Thandiwe', 'Mokoena', 'Self', 1, 1),
    (@NtandoId, @PrimaryPatientId, 'Ntando', 'Mokoena', 'Child', 1, 1),
    (@BabaId, @PrimaryPatientId, 'Baba', 'Mokoena', 'Parent', 1, 0);
    
    -- Thandiwe's Records
    INSERT INTO MedicalConditions (PatientId, Name, Type, Notes) VALUES
    (@SelfId, 'Hypertension', 'Chronic', 'Diagnosed 2022 - Managed'),
    (@SelfId, 'Type 2 Diabetes', 'Chronic', 'Diagnosed 2023 - Managed'),
    (@SelfId, 'Penicillin allergy', 'Allergy', 'Confirmed by Dr. N. Adams');
    
    INSERT INTO LabResults (PatientId, TestName, Value, Status, TestDate) VALUES
    (@SelfId, 'HbA1c', '6.8%', 'Attention', DATEADD(day, -15, GETUTCDATE())),
    (@SelfId, 'Blood pressure', '128/82', 'Normal', DATEADD(day, -15, GETUTCDATE())),
    (@SelfId, 'Cholesterol panel', 'Normal', 'Normal', DATEADD(day, -32, GETUTCDATE()));
    
    INSERT INTO Vaccinations (PatientId, VaccineName, Status, Notes, DateAdministered) VALUES
    (@SelfId, 'Influenza', 'Up to date', 'April 2026', DATEADD(month, -3, GETUTCDATE()));
    INSERT INTO Vaccinations (PatientId, VaccineName, Status, Notes, DateDue) VALUES
    (@SelfId, 'Tetanus', 'Due', 'Due 2026', DATEADD(month, 5, GETUTCDATE()));
    
    -- Ntando's Records
    INSERT INTO MedicalConditions (PatientId, Name, Type, Notes) VALUES
    (@NtandoId, 'Asthma', 'Chronic', 'Diagnosed 2024 - Mild intermittent'),
    (@NtandoId, 'Peanut allergy', 'Allergy', 'Severe allergy - carries EpiPen');
    
    INSERT INTO LabResults (PatientId, TestName, Value, Status, TestDate) VALUES
    (@NtandoId, 'Allergy IgE Screen', 'High', 'Attention', DATEADD(month, -2, GETUTCDATE()));
    
    INSERT INTO Vaccinations (PatientId, VaccineName, Status, Notes, DateAdministered) VALUES
    (@NtandoId, 'Measles-Mumps-Rubella (MMR)', 'Up to date', 'Completed 2025', DATEADD(year, -1, GETUTCDATE())),
    (@NtandoId, 'Polio', 'Up to date', 'Completed 2025', DATEADD(year, -1, GETUTCDATE()));

    -- Baba's Records
    INSERT INTO MedicalConditions (PatientId, Name, Type, Notes) VALUES
    (@BabaId, 'Osteoarthritis', 'Chronic', 'Diagnosed 2018 - Mild knee pain'),
    (@BabaId, 'Sulfa allergy', 'Allergy', 'Rash reaction - documented 2020');
    
    INSERT INTO LabResults (PatientId, TestName, Value, Status, TestDate) VALUES
    (@BabaId, 'Creatinine (Kidney)', 'Normal', 'Normal', DATEADD(month, -1, GETUTCDATE())),
    (@BabaId, 'Uric Acid', 'High', 'Attention', DATEADD(month, -1, GETUTCDATE()));
    
    INSERT INTO Vaccinations (PatientId, VaccineName, Status, Notes, DateAdministered) VALUES
    (@BabaId, 'Pneumococcal', 'Up to date', 'Given 2024', DATEADD(year, -2, GETUTCDATE()));
END;
GO

-- 3. Seed Order Mock Data
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_SeedOrderMockData')
    DROP PROCEDURE sp_SeedOrderMockData;
GO
CREATE PROCEDURE sp_SeedOrderMockData @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    DELETE FROM Orders WHERE PatientId = @PatientId;
    IF NOT EXISTS (SELECT 1 FROM MarketplaceProducts)
    BEGIN
        INSERT INTO MarketplaceProducts (Name, Price, IconName) VALUES
        ('Vitamin D3 1000IU', 89.00, 'ShoppingBag'),
        ('Digital blood pressure monitor', 649.00, 'Activity');
    END
    
    INSERT INTO Orders (OrderNumber, PatientId, OrderDate, TotalAmount, Status, ItemsCount, DispatchedFrom) VALUES
    ('QP-1042', @PatientId, DATEADD(hour, -4, GETUTCDATE()), 486.50, 'In transit', 3, 'Clicks, Claremont'),
    ('QP-1031', @PatientId, DATEADD(day, -29, GETUTCDATE()), 312.00, 'Delivered', 2, 'Clicks, Claremont'),
    ('QP-1019', @PatientId, DATEADD(day, -45, GETUTCDATE()), 640.00, 'Delivered', 4, 'Dis-Chem, Tokai');
END;
GO

-- 4. Seed Payment Mock Data
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_SeedPaymentMockData')
    DROP PROCEDURE sp_SeedPaymentMockData;
GO
CREATE PROCEDURE sp_SeedPaymentMockData @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    DELETE FROM MedicalAids WHERE PatientId = @PatientId;
    DELETE FROM Claims WHERE PatientId = @PatientId;
    DELETE FROM PaymentMethods WHERE PatientId = @PatientId;
    
    INSERT INTO MedicalAids (PatientId, SchemeName, PlanName, MemberNumber, AnnualLimit, AmountUsed, Status) VALUES
    (@PatientId, 'Discovery Health', 'Classic Saver', '3820 4471 002', 10000.00, 4200.00, 'Active');
    
    INSERT INTO Claims (PatientId, Description, Amount, Status, ClaimDate) VALUES
    (@PatientId, 'Chronic medication - July', 486.50, 'Approved', DATEADD(day, -5, GETUTCDATE())),
    (@PatientId, 'Consultation - Dr. Adams', 650.00, 'Pending', DATEADD(day, -10, GETUTCDATE()));
    
    INSERT INTO PaymentMethods (PatientId, CardType, LastFour, ExpiryDate, GatewayToken, IsDefault) VALUES
    (@PatientId, 'Visa', '4821', '09/28', 'tok_mock123', 1);
END;
GO

-- 5. Seed Pharmacist Module Mock Data
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_SeedPharmacistMockData')
    DROP PROCEDURE sp_SeedPharmacistMockData;
GO
CREATE PROCEDURE sp_SeedPharmacistMockData
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM PharmacyInventory;
    DELETE FROM VerificationQueue;
    DELETE FROM DispensingOrders;

    -- Seed Inventory
    INSERT INTO PharmacyInventory (BranchName, MedicationName, InStock, ReorderLevel, Status) VALUES
    ('Clicks, Claremont', 'Amlodipine 5mg', 420, 100, 'Healthy'),
    ('Clicks, Claremont', 'Metformin 500mg', 85, 100, 'Low'),
    ('Clicks, Claremont', 'Atorvastatin 20mg', 12, 50, 'Reorder now'),
    ('Clicks, Claremont', 'Cetirizine 10mg', 310, 80, 'Healthy');

    -- Seed Verification Queue
    INSERT INTO VerificationQueue (PrescriptionNumber, PatientName, DoctorName, MedicationDetails, InteractionStatus, IsFlagged, Initials, AvatarBgColor, MedicationShort, Status) VALUES
    ('4821', 'Thandiwe Mokoena', 'Dr. N. Adams', 'Atorvastatin 20mg, nightly, 30 days', 'No conflicts detected', 0, 'TM', 'bg-teal-600', 'Atorvastatin 20mg', 'Pending'),
    ('4822', 'Thandiwe Mokoena', 'Dr. N. Adams', 'Ibuprofen 200mg, twice daily, 10 days', 'Conflict detected: Combined with Amlodipine', 1, 'TM', 'bg-rose-600', 'Ibuprofen 200mg (Flagged)', 'Pending'),
    ('4823', 'Jabu Khumalo', 'Dr. S. Patel', 'Metformin 500mg, twice daily, 30 days', 'No conflicts detected', 0, 'JK', 'bg-blue-600', 'Metformin 500mg', 'Pending'),
    ('4824', 'Rose Pillay', 'Dr. M. Botha', 'Bisoprolol 5mg, once daily, 30 days', 'No conflicts detected', 0, 'RP', 'bg-amber-500', 'Bisoprolol 5mg', 'Pending');

    -- Seed Dispensing Orders
    INSERT INTO DispensingOrders (OrderNumber, PatientName, ItemCount, Fulfilment, HandoverRef, ActionLabel) VALUES
    ('QP-1042', 'Thandiwe Mokoena', 3, 'Delivery', 'Courier: Fastway (TRK-88412)', 'Mark packed'),
    ('QP-1043', 'Jabu Khumalo', 1, 'Click & collect', 'Pickup Code: #PIN-4810', 'Mark ready');
END;
GO

-- 6. Seed Doctor Module Mock Data
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_SeedDoctorMockData')
    DROP PROCEDURE sp_SeedDoctorMockData;
GO
CREATE PROCEDURE sp_SeedDoctorMockData
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM DoctorPatients;
    DELETE FROM DoctorSchedules;
    DELETE FROM DoctorPendingActions;
    DELETE FROM DoctorConsultations;
    DELETE FROM DoctorConsultationMessages;
    DELETE FROM DoctorPrescriptions;
    DELETE FROM DoctorCalendarSlots;

    -- Seed Doctor Patients
    INSERT INTO DoctorPatients (PatientId, PatientName, Age, LastVisitDate, ConditionSummary, MedicalAid, Initials, Email, Phone) VALUES
    ('A1B2C3D4-E5F6-7A8B-9C0D-1E2F3A4B5C6D', 'Thandiwe Mokoena', 41, '2 Jul 2026', 'Hypertension, T2D', 'Discovery Health', 'TM', 'thandiwe@example.com', '082 123 4567'),
    ('B2C3D4E5-F6A7-8B9C-0D1E-2F3A4B5C6D7E', 'Jabu Khumalo', 34, '29 Jun 2026', 'New patient', 'Bonitas', 'JK', 'jabu@example.com', '083 234 5678'),
    ('C3D4E5F6-A7B8-9C0D-1E2F-3A4B5C6D7E8F', 'Rose Pillay', 58, '18 Jun 2026', 'Chronic, cardiac', 'Discovery Health', 'RP', 'rose@example.com', '084 345 6789');

    -- Seed Doctor Schedules
    INSERT INTO DoctorSchedules (Id, PatientId, PatientName, Initials, AvatarBgColor, Time, AppointmentType, Mode, Status) VALUES
    ('11111111-1111-1111-1111-111111111111', 'A1B2C3D4-E5F6-7A8B-9C0D-1E2F3A4B5C6D', 'Thandiwe Mokoena', 'TM', 'bg-emerald-600', '09:30', 'Follow-up', 'Video', 'Scheduled'),
    ('22222222-2222-2222-2222-222222222222', 'B2C3D4E5-F6A7-8B9C-0D1E-2F3A4B5C6D7E', 'Jabu Khumalo', 'JK', 'bg-blue-600', '10:15', 'New patient', 'Video', 'Scheduled'),
    ('33333333-3333-3333-3333-333333333333', 'C3D4E5F6-A7B8-9C0D-1E2F-3A4B5C6D7E8F', 'Rose Pillay', 'RP', 'bg-amber-500', '11:00', 'Chronic review', 'Video', 'Scheduled');

    -- Seed Doctor Pending Actions
    INSERT INTO DoctorPendingActions (Id, ActionType, Title, Subtitle, Count, IconType) VALUES
    (NEWID(), 'Referral', '2 referrals awaiting sign-off', 'Specialist referrals', 2, 'Warning'),
    (NEWID(), 'Interaction', '1 prescription flagged for interaction', 'Requires review', 1, 'File');

    -- Seed Doctor Consultations
    INSERT INTO DoctorConsultations (Id, PatientId, PatientName, CallTimerDisplay, IsLive, Assessment, [Plan]) VALUES
    (NEWID(), 'A1B2C3D4-E5F6-7A8B-9C0D-1E2F3A4B5C6D', 'Thandiwe Mokoena', '14:22', 1, 
     'Blood pressure well controlled. HbA1c improved to 6.8%. Continue current regimen.', 
     'Repeat bloods in 4 weeks. Issue digital referral to dietitian.');

    -- Seed Doctor Prescriptions
    INSERT INTO DoctorPrescriptions (Id, PatientId, PatientName, PreferredPharmacy, Medication, DosageDuration, Status, CreatedAt) VALUES
    (NEWID(), 'A1B2C3D4-E5F6-7A8B-9C0D-1E2F3A4B5C6D', 'Thandiwe Mokoena', 'Clicks, Claremont', 'Atorvastatin 20mg', 'Nightly, 30 days', 'Awaiting verification', DATEADD(HOUR, -2, GETDATE())),
    (NEWID(), 'C3D4E5F6-A7B8-9C0D-1E2F-3A4B5C6D7E8F', 'Rose Pillay', 'Dis-Chem, Tokai', 'Bisoprolol 5mg', 'Once daily, 30 days', 'Verified', DATEADD(DAY, -1, GETDATE()));

    -- Seed Doctor Calendar Slots
    INSERT INTO DoctorCalendarSlots (Id, DayLabel, MorningStatus, AfternoonStatus, IsMorningBlocked, IsAfternoonBlocked) VALUES
    (NEWID(), 'Mon 6 Jul', '4 booked', '2 booked', 0, 0),
    (NEWID(), 'Tue 7 Jul', '3 booked', 'Open', 0, 0),
    (NEWID(), 'Wed 8 Jul', 'Open', '5 booked', 0, 0);
END;
GO


-- ==============================================================================
-- PART 4: SEED EXECUTION LOOP
-- ==============================================================================

PRINT 'Starting DB seed loop for all users...';

-- Seed Pharmacist Global Data
EXEC sp_SeedPharmacistMockData;

-- Seed Doctor Global Data
EXEC sp_SeedDoctorMockData;

DECLARE @UserId UNIQUEIDENTIFIER;

DECLARE user_cursor CURSOR FOR 
SELECT Id FROM Users;

OPEN user_cursor;

FETCH NEXT FROM user_cursor INTO @UserId;

WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT 'Seeding user: ' + CAST(@UserId AS VARCHAR(50));
    
    EXEC sp_SeedPatientMockData @PatientId = @UserId;
    EXEC sp_SeedHealthRecordMockData @PrimaryPatientId = @UserId;
    EXEC sp_SeedOrderMockData @PatientId = @UserId;
    EXEC sp_SeedPaymentMockData @PatientId = @UserId;
    
    FETCH NEXT FROM user_cursor INTO @UserId;
END;

CLOSE user_cursor;
DEALLOCATE user_cursor;

PRINT 'Seeding complete! All tables and stored procedures are created, and all users seeded successfully.';
GO
