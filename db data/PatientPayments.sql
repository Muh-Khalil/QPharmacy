-- Create MedicalAids Table
CREATE TABLE MedicalAids (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PatientId UNIQUEIDENTIFIER NOT NULL,
    SchemeName NVARCHAR(100) NOT NULL,
    PlanName NVARCHAR(100) NOT NULL,
    MemberNumber NVARCHAR(50) NOT NULL,
    AnnualLimit DECIMAL(18, 2) NOT NULL,
    AmountUsed DECIMAL(18, 2) NOT NULL,
    Status NVARCHAR(50) NOT NULL, -- 'Active', 'Suspended', 'Expired'
    CreatedAt DATETIME DEFAULT GETDATE(),
    UpdatedAt DATETIME NULL,
    FOREIGN KEY (PatientId) REFERENCES Users(Id)
);
GO

-- Create Claims Table
CREATE TABLE Claims (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PatientId UNIQUEIDENTIFIER NOT NULL,
    Description NVARCHAR(255) NOT NULL,
    Amount DECIMAL(18, 2) NOT NULL,
    Status NVARCHAR(50) NOT NULL, -- 'Approved', 'Pending', 'Rejected'
    ClaimDate DATETIME DEFAULT GETDATE(),
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (PatientId) REFERENCES Users(Id)
);
GO

-- Create PaymentMethods Table
CREATE TABLE PaymentMethods (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PatientId UNIQUEIDENTIFIER NOT NULL,
    CardType NVARCHAR(50) NOT NULL, -- 'Visa', 'Mastercard', etc.
    LastFour NVARCHAR(4) NOT NULL,
    ExpiryDate NVARCHAR(10) NOT NULL, -- 'MM/YY' or 'MM/YYYY'
    GatewayToken NVARCHAR(255) NOT NULL,
    IsDefault BIT DEFAULT 0,
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (PatientId) REFERENCES Users(Id)
);
GO

-- Stored Procedures

-- 1. Get Medical Aid Details
CREATE PROCEDURE sp_GetMedicalAidDetails
    @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        Id,
        PatientId,
        SchemeName,
        PlanName,
        MemberNumber,
        AnnualLimit,
        AmountUsed,
        Status,
        CreatedAt,
        UpdatedAt
    FROM MedicalAids
    WHERE PatientId = @PatientId;
END;
GO

-- 2. Get Recent Claims
CREATE PROCEDURE sp_GetRecentClaims
    @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        Id,
        PatientId,
        Description,
        Amount,
        Status,
        ClaimDate,
        CreatedAt
    FROM Claims
    WHERE PatientId = @PatientId
    ORDER BY ClaimDate DESC;
END;
GO

-- 3. Get Saved Payment Methods
CREATE PROCEDURE sp_GetSavedPaymentMethods
    @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        Id,
        PatientId,
        CardType,
        LastFour,
        ExpiryDate,
        GatewayToken,
        IsDefault,
        CreatedAt
    FROM PaymentMethods
    WHERE PatientId = @PatientId
    ORDER BY IsDefault DESC, CreatedAt DESC;
END;
GO

-- 4. Seed Payment Mock Data
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_SeedPaymentMockData')
    DROP PROCEDURE sp_SeedPaymentMockData;
GO

CREATE PROCEDURE sp_SeedPaymentMockData
    @PatientId UNIQUEIDENTIFIER
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
