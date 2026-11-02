-- ==============================================================================
-- Tables for Patient Health Records Feature
-- ==============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FamilyMembers')
BEGIN
    CREATE TABLE FamilyMembers (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PrimaryPatientId UNIQUEIDENTIFIER NOT NULL, -- FK to Users (primary patient account)
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        Relationship NVARCHAR(50) NOT NULL, -- 'Self', 'Child', 'Parent', etc.
        CanView BIT NOT NULL DEFAULT 1,
        CanEdit BIT NOT NULL DEFAULT 0
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MedicalConditions')
BEGIN
    CREATE TABLE MedicalConditions (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        PatientId UNIQUEIDENTIFIER NOT NULL, -- Belongs to a family member (or primary patient as a family member entry)
        Name NVARCHAR(255) NOT NULL,
        Type NVARCHAR(50) NOT NULL, -- 'Chronic', 'Allergy'
        Notes NVARCHAR(500) NOT NULL, -- e.g., 'Diagnosed 2022 - Managed', 'Confirmed by Dr. N. Adams'
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
        Value NVARCHAR(100) NOT NULL, -- e.g., '6.8%', '128/82', 'Normal'
        Status NVARCHAR(50) NOT NULL, -- 'Normal', 'Attention'
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
        Status NVARCHAR(50) NOT NULL, -- 'Up to date', 'Due'
        Notes NVARCHAR(100) NOT NULL, -- e.g., 'April 2026', 'Due 2026'
        DateAdministered DATETIME2 NULL,
        DateDue DATETIME2 NULL
    );
END
GO

-- ==============================================================================
-- Stored Procedures
-- ==============================================================================

-- GET FAMILY MEMBERS
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetFamilyMembers')
    DROP PROCEDURE sp_GetFamilyMembers;
GO

CREATE PROCEDURE sp_GetFamilyMembers
    @PrimaryPatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        Id,
        PrimaryPatientId,
        FirstName,
        LastName,
        Relationship,
        CanView,
        CanEdit
    FROM FamilyMembers
    WHERE PrimaryPatientId = @PrimaryPatientId;
END
GO

-- GET MEDICAL CONDITIONS & ALLERGIES
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetMedicalConditions')
    DROP PROCEDURE sp_GetMedicalConditions;
GO

CREATE PROCEDURE sp_GetMedicalConditions
    @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        Id,
        PatientId,
        Name,
        Type,
        Notes,
        RecordedAt
    FROM MedicalConditions
    WHERE PatientId = @PatientId
    ORDER BY RecordedAt DESC;
END
GO

-- GET LAB RESULTS
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetLabResults')
    DROP PROCEDURE sp_GetLabResults;
GO

CREATE PROCEDURE sp_GetLabResults
    @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        Id,
        PatientId,
        TestName,
        Value,
        Status,
        TestDate
    FROM LabResults
    WHERE PatientId = @PatientId
    ORDER BY TestDate DESC;
END
GO

-- GET VACCINATION HISTORY
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetVaccinationHistory')
    DROP PROCEDURE sp_GetVaccinationHistory;
GO

CREATE PROCEDURE sp_GetVaccinationHistory
    @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        Id,
        PatientId,
        VaccineName,
        Status,
        Notes,
        DateAdministered,
        DateDue
    FROM Vaccinations
    WHERE PatientId = @PatientId
    ORDER BY COALESCE(DateAdministered, DateDue) DESC;
END
GO

-- SEED MOCK DATA (Optional - For Development/Initial SQL seeding)
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_SeedHealthRecordMockData')
    DROP PROCEDURE sp_SeedHealthRecordMockData;
GO

CREATE PROCEDURE sp_SeedHealthRecordMockData
    @PrimaryPatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Clear if already exist to re-seed cleanly
    DELETE FROM FamilyMembers WHERE PrimaryPatientId = @PrimaryPatientId;
    
    -- 1. Create family members
    DECLARE @SelfId UNIQUEIDENTIFIER = NEWID();
    DECLARE @NtandoId UNIQUEIDENTIFIER = NEWID();
    DECLARE @BabaId UNIQUEIDENTIFIER = NEWID();
    
    INSERT INTO FamilyMembers (Id, PrimaryPatientId, FirstName, LastName, Relationship, CanView, CanEdit) VALUES
    (@SelfId, @PrimaryPatientId, 'Thandiwe', 'Mokoena', 'Self', 1, 1),
    (@NtandoId, @PrimaryPatientId, 'Ntando', 'Mokoena', 'Child', 1, 1),
    (@BabaId, @PrimaryPatientId, 'Baba', 'Mokoena', 'Parent', 1, 0);
    
    -- 2. Seed Thandiwe's Records
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
    
    -- 3. Seed Ntando's Records
    INSERT INTO MedicalConditions (PatientId, Name, Type, Notes) VALUES
    (@NtandoId, 'Asthma', 'Chronic', 'Diagnosed 2024 - Mild intermittent'),
    (@NtandoId, 'Peanut allergy', 'Allergy', 'Severe allergy - carries EpiPen');
    
    INSERT INTO LabResults (PatientId, TestName, Value, Status, TestDate) VALUES
    (@NtandoId, 'Allergy IgE Screen', 'High', 'Attention', DATEADD(month, -2, GETUTCDATE()));
    
    INSERT INTO Vaccinations (PatientId, VaccineName, Status, Notes, DateAdministered) VALUES
    (@NtandoId, 'Measles-Mumps-Rubella (MMR)', 'Up to date', 'Completed 2025', DATEADD(year, -1, GETUTCDATE())),
    (@NtandoId, 'Polio', 'Up to date', 'Completed 2025', DATEADD(year, -1, GETUTCDATE()));

    -- 4. Seed Baba's Records
    INSERT INTO MedicalConditions (PatientId, Name, Type, Notes) VALUES
    (@BabaId, 'Osteoarthritis', 'Chronic', 'Diagnosed 2018 - Mild knee pain'),
    (@BabaId, 'Sulfa allergy', 'Allergy', 'Rash reaction - documented 2020');
    
    INSERT INTO LabResults (PatientId, TestName, Value, Status, TestDate) VALUES
    (@BabaId, 'Creatinine (Kidney)', 'Normal', 'Normal', DATEADD(month, -1, GETUTCDATE())),
    (@BabaId, 'Uric Acid', 'High', 'Attention', DATEADD(month, -1, GETUTCDATE()));
    
    INSERT INTO Vaccinations (PatientId, VaccineName, Status, Notes, DateAdministered) VALUES
    (@BabaId, 'Pneumococcal', 'Up to date', 'Given 2024', DATEADD(year, -2, GETUTCDATE()));
END
GO
