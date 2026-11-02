SET XACT_ABORT ON;
GO

IF OBJECT_ID('dbo.PharmacyBranches', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PharmacyBranches
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PharmacyBranches PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL CONSTRAINT UQ_PharmacyBranches_Name UNIQUE,
        Address NVARCHAR(500) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_PharmacyBranches_IsActive DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_PharmacyBranches_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END;
GO

IF OBJECT_ID('dbo.MedicationCatalogue', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MedicationCatalogue
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MedicationCatalogue PRIMARY KEY,
        GenericName NVARCHAR(200) NOT NULL,
        BrandName NVARCHAR(200) NULL,
        Strength NVARCHAR(100) NULL,
        Form NVARCHAR(100) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_MedicationCatalogue_IsActive DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_MedicationCatalogue_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_MedicationCatalogue UNIQUE (GenericName, BrandName, Strength, Form)
    );
END;
GO

IF COL_LENGTH('dbo.Users', 'ProfessionalRegistrationNumber') IS NULL
    ALTER TABLE dbo.Users ADD ProfessionalRegistrationNumber NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.Users', 'OrganizationOrBranch') IS NULL
    ALTER TABLE dbo.Users ADD OrganizationOrBranch NVARCHAR(255) NULL;
GO

IF OBJECT_ID('dbo.WorkflowPrescriptions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkflowPrescriptions
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkflowPrescriptions PRIMARY KEY,
        PrescriptionNumber NVARCHAR(50) NOT NULL CONSTRAINT UQ_WorkflowPrescriptions_Number UNIQUE,
        PatientId UNIQUEIDENTIFIER NOT NULL,
        DoctorId UNIQUEIDENTIFIER NOT NULL,
        PatientName NVARCHAR(150) NOT NULL,
        DoctorName NVARCHAR(150) NOT NULL,
        PreferredPharmacy NVARCHAR(200) NOT NULL CONSTRAINT DF_WorkflowPrescriptions_Pharmacy DEFAULT '',
        Status NVARCHAR(40) NOT NULL,
        IssuedAt DATETIME2 NOT NULL,
        SignedAt DATETIME2 NULL,
        SignatureHash NVARCHAR(128) NOT NULL CONSTRAINT DF_WorkflowPrescriptions_Signature DEFAULT '',
        UpdatedAt DATETIME2 NOT NULL,
        Version INT NOT NULL CONSTRAINT DF_WorkflowPrescriptions_Version DEFAULT 1,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_WorkflowPrescriptions_Status CHECK
            (Status IN ('Draft','AwaitingVerification','Queried','Approved','Rejected','Cancelled'))
    );
    CREATE INDEX IX_WorkflowPrescriptions_Patient ON dbo.WorkflowPrescriptions(PatientId, IssuedAt DESC);
    CREATE INDEX IX_WorkflowPrescriptions_Queue ON dbo.WorkflowPrescriptions(Status, IssuedAt);
END;
GO

IF COL_LENGTH('dbo.WorkflowPrescriptions', 'SignedAt') IS NULL
    ALTER TABLE dbo.WorkflowPrescriptions ADD SignedAt DATETIME2 NULL;
IF COL_LENGTH('dbo.WorkflowPrescriptions', 'SignatureHash') IS NULL
    ALTER TABLE dbo.WorkflowPrescriptions ADD SignatureHash NVARCHAR(128) NOT NULL CONSTRAINT DF_WorkflowPrescriptions_SignatureUpgrade DEFAULT '';
GO

IF OBJECT_ID('dbo.WorkflowPrescriptionItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkflowPrescriptionItems
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkflowPrescriptionItems PRIMARY KEY,
        PrescriptionId UNIQUEIDENTIFIER NOT NULL,
        MedicationName NVARCHAR(200) NOT NULL,
        Dosage NVARCHAR(200) NOT NULL,
        Frequency NVARCHAR(100) NOT NULL,
        DurationDays INT NOT NULL,
        Quantity INT NOT NULL,
        RepeatsAllowed INT NOT NULL CONSTRAINT DF_WorkflowPrescriptionItems_Repeats DEFAULT 0,
        RepeatsRemaining INT NOT NULL CONSTRAINT DF_WorkflowPrescriptionItems_RepeatsRemaining DEFAULT 0,
        CONSTRAINT FK_WorkflowPrescriptionItems_Prescription FOREIGN KEY (PrescriptionId)
            REFERENCES dbo.WorkflowPrescriptions(Id),
        CONSTRAINT CK_WorkflowPrescriptionItems_Quantity CHECK (Quantity > 0)
    );
END;
GO

IF COL_LENGTH('dbo.WorkflowPrescriptionItems', 'RepeatsRemaining') IS NULL
    ALTER TABLE dbo.WorkflowPrescriptionItems ADD RepeatsRemaining INT NOT NULL CONSTRAINT DF_WorkflowPrescriptionItems_RepeatsRemainingUpgrade DEFAULT 0;
GO

IF OBJECT_ID('dbo.PrescriptionReviews', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PrescriptionReviews
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PrescriptionReviews PRIMARY KEY,
        PrescriptionId UNIQUEIDENTIFIER NOT NULL,
        ActorUserId UNIQUEIDENTIFIER NOT NULL,
        Decision NVARCHAR(40) NOT NULL,
        Note NVARCHAR(2000) NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT FK_PrescriptionReviews_Prescription FOREIGN KEY (PrescriptionId)
            REFERENCES dbo.WorkflowPrescriptions(Id)
    );
END;
GO

IF OBJECT_ID('dbo.PrescriptionStatusHistory', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PrescriptionStatusHistory
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PrescriptionStatusHistory PRIMARY KEY,
        PrescriptionId UNIQUEIDENTIFIER NOT NULL,
        FromStatus NVARCHAR(40) NOT NULL,
        ToStatus NVARCHAR(40) NOT NULL,
        ChangedByUserId UNIQUEIDENTIFIER NOT NULL,
        Reason NVARCHAR(2000) NOT NULL,
        ChangedAt DATETIME2 NOT NULL,
        CONSTRAINT FK_PrescriptionStatusHistory_Prescription FOREIGN KEY (PrescriptionId)
            REFERENCES dbo.WorkflowPrescriptions(Id)
    );
END;
GO

IF OBJECT_ID('dbo.InventoryReservations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryReservations
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_InventoryReservations PRIMARY KEY,
        PrescriptionId UNIQUEIDENTIFIER NOT NULL,
        OrderId UNIQUEIDENTIFIER NOT NULL,
        InventoryItemId UNIQUEIDENTIFIER NOT NULL,
        Quantity INT NOT NULL,
        Status NVARCHAR(30) NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        ReleasedAt DATETIME2 NULL,
        CONSTRAINT FK_InventoryReservations_Prescription FOREIGN KEY (PrescriptionId)
            REFERENCES dbo.WorkflowPrescriptions(Id),
        CONSTRAINT FK_InventoryReservations_Inventory FOREIGN KEY (InventoryItemId)
            REFERENCES dbo.PharmacyInventory(Id),
        CONSTRAINT CK_InventoryReservations_Quantity CHECK (Quantity > 0)
    );
    CREATE INDEX IX_InventoryReservations_Prescription ON dbo.InventoryReservations(PrescriptionId, OrderId, Status);
END;
GO

IF COL_LENGTH('dbo.InventoryReservations', 'OrderId') IS NULL
BEGIN
    ALTER TABLE dbo.InventoryReservations ADD OrderId UNIQUEIDENTIFIER NULL;
    UPDATE dbo.InventoryReservations SET OrderId = PrescriptionId WHERE OrderId IS NULL;
    ALTER TABLE dbo.InventoryReservations ALTER COLUMN OrderId UNIQUEIDENTIFIER NOT NULL;
END;
GO

IF OBJECT_ID('dbo.InventoryTransactions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryTransactions
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_InventoryTransactions PRIMARY KEY,
        InventoryItemId UNIQUEIDENTIFIER NOT NULL,
        PrescriptionId UNIQUEIDENTIFIER NOT NULL,
        TransactionType NVARCHAR(30) NOT NULL,
        QuantityChange INT NOT NULL,
        PerformedBy UNIQUEIDENTIFIER NOT NULL,
        Notes NVARCHAR(500) NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT FK_InventoryTransactions_Inventory FOREIGN KEY (InventoryItemId) REFERENCES dbo.PharmacyInventory(Id),
        CONSTRAINT FK_InventoryTransactions_Prescription FOREIGN KEY (PrescriptionId) REFERENCES dbo.WorkflowPrescriptions(Id)
    );
END;
GO

IF OBJECT_ID('dbo.FulfilmentItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FulfilmentItems
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_FulfilmentItems PRIMARY KEY,
        OrderId UNIQUEIDENTIFIER NOT NULL,
        PrescriptionId UNIQUEIDENTIFIER NOT NULL,
        PrescriptionItemId UNIQUEIDENTIFIER NOT NULL,
        RequestedMedication NVARCHAR(200) NOT NULL,
        DispensedMedication NVARCHAR(200) NOT NULL,
        RequestedQuantity INT NOT NULL,
        FulfilledQuantity INT NOT NULL,
        IsSubstitution BIT NOT NULL CONSTRAINT DF_FulfilmentItems_Substitution DEFAULT 0,
        SubstitutionReason NVARCHAR(500) NOT NULL CONSTRAINT DF_FulfilmentItems_Reason DEFAULT '',
        Status NVARCHAR(30) NOT NULL,
        CONSTRAINT FK_FulfilmentItems_Prescription FOREIGN KEY (PrescriptionId) REFERENCES dbo.WorkflowPrescriptions(Id),
        CONSTRAINT FK_FulfilmentItems_PrescriptionItem FOREIGN KEY (PrescriptionItemId) REFERENCES dbo.WorkflowPrescriptionItems(Id)
    );
    CREATE INDEX IX_FulfilmentItems_Order ON dbo.FulfilmentItems(OrderId);
END;
GO
