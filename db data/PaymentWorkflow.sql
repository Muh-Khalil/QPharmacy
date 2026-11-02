-- =====================================================================
-- Phase 4: Payments and Medical Aid Schema Migration
-- =====================================================================

-- 1. Add PaymentStatus column to Orders if not present
IF COL_LENGTH('dbo.Orders', 'PaymentStatus') IS NULL
BEGIN
    ALTER TABLE dbo.Orders ADD PaymentStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_Orders_PaymentStatus DEFAULT 'Unpaid';
    EXEC('UPDATE dbo.Orders SET PaymentStatus = ''Paid'' WHERE Status IN (''Delivered'', ''In transit'')');
END;
GO

-- 2. Add PaymentStatus column to DispensingOrders if not present
IF COL_LENGTH('dbo.DispensingOrders', 'PaymentStatus') IS NULL
BEGIN
    ALTER TABLE dbo.DispensingOrders ADD PaymentStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_DispensingOrders_PaymentStatus DEFAULT 'Unpaid';
    EXEC('UPDATE d SET d.PaymentStatus = ''Paid'' FROM dbo.DispensingOrders d JOIN dbo.Orders o ON o.Id = d.Id OR o.OrderNumber = d.OrderNumber WHERE o.PaymentStatus = ''Paid''');
END;
GO

-- 3. Create PaymentIntents Table
IF OBJECT_ID('dbo.PaymentIntents', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PaymentIntents
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PaymentIntents PRIMARY KEY DEFAULT NEWID(),
        OrderId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_PaymentIntents_Orders FOREIGN KEY REFERENCES dbo.Orders(Id),
        PatientId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_PaymentIntents_Users FOREIGN KEY REFERENCES dbo.Users(Id),
        Amount DECIMAL(18,2) NOT NULL,
        Currency NVARCHAR(3) NOT NULL CONSTRAINT DF_PaymentIntents_Currency DEFAULT 'ZAR',
        PaymentMethod NVARCHAR(50) NOT NULL, -- 'Card', 'MedicalAid', 'Split'
        Status NVARCHAR(50) NOT NULL,        -- 'Pending', 'Processing', 'Succeeded', 'Failed', 'Refunded'
        GatewayTransactionId NVARCHAR(100) NULL,
        IdempotencyKey NVARCHAR(100) NOT NULL CONSTRAINT UQ_PaymentIntents_Idempotency UNIQUE,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_PaymentIntents_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_PaymentIntents_UpdatedAt DEFAULT SYSUTCDATETIME()
    );
END;
GO

-- 4. Create PaymentAllocations Table (Supports Split & Single Payments)
IF OBJECT_ID('dbo.PaymentAllocations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PaymentAllocations
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PaymentAllocations PRIMARY KEY DEFAULT NEWID(),
        PaymentIntentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_PaymentAllocations_Intent FOREIGN KEY REFERENCES dbo.PaymentIntents(Id),
        OrderId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_PaymentAllocations_Orders FOREIGN KEY REFERENCES dbo.Orders(Id),
        Source NVARCHAR(50) NOT NULL, -- 'Card', 'MedicalAid'
        Amount DECIMAL(18,2) NOT NULL,
        Status NVARCHAR(50) NOT NULL CONSTRAINT DF_PaymentAllocations_Status DEFAULT 'Allocated', -- 'Allocated', 'Reversed'
        ClaimId UNIQUEIDENTIFIER NULL CONSTRAINT FK_PaymentAllocations_Claims FOREIGN KEY REFERENCES dbo.Claims(Id),
        PaymentMethodId UNIQUEIDENTIFIER NULL CONSTRAINT FK_PaymentAllocations_PaymentMethods FOREIGN KEY REFERENCES dbo.PaymentMethods(Id),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_PaymentAllocations_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END;
GO

-- 5. Create WebhookEvents Table (Idempotent Webhook Engine)
IF OBJECT_ID('dbo.WebhookEvents', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WebhookEvents
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WebhookEvents PRIMARY KEY DEFAULT NEWID(),
        EventId NVARCHAR(100) NOT NULL CONSTRAINT UQ_WebhookEvents_EventId UNIQUE,
        EventType NVARCHAR(100) NOT NULL,
        Signature NVARCHAR(255) NOT NULL,
        Payload NVARCHAR(MAX) NOT NULL,
        Status NVARCHAR(50) NOT NULL, -- 'Processed', 'Duplicate', 'Failed'
        ProcessedAt DATETIME2 NOT NULL CONSTRAINT DF_WebhookEvents_ProcessedAt DEFAULT SYSUTCDATETIME()
    );
END;
GO

-- 6. Ensure default Medical Aid and Saved Card for Thandiwe and Jabu
IF NOT EXISTS (SELECT 1 FROM dbo.MedicalAids WHERE PatientId = 'a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d')
BEGIN
    INSERT INTO dbo.MedicalAids (Id, PatientId, SchemeName, PlanName, MemberNumber, AnnualLimit, AmountUsed, Status, CreatedAt)
    VALUES (NEWID(), 'a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d', 'Discovery Health', 'Classic Saver', 'DH-9823412', 12000.00, 3200.00, 'Active', GETDATE());
END;

IF NOT EXISTS (SELECT 1 FROM dbo.MedicalAids WHERE PatientId = 'b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e')
BEGIN
    INSERT INTO dbo.MedicalAids (Id, PatientId, SchemeName, PlanName, MemberNumber, AnnualLimit, AmountUsed, Status, CreatedAt)
    VALUES (NEWID(), 'b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e', 'Bonitas', 'BonComprehensive', 'BON-772183', 15000.00, 4500.00, 'Active', GETDATE());
END;

IF NOT EXISTS (SELECT 1 FROM dbo.PaymentMethods WHERE PatientId = 'a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d')
BEGIN
    INSERT INTO dbo.PaymentMethods (Id, PatientId, CardType, LastFour, ExpiryDate, GatewayToken, IsDefault, CreatedAt)
    VALUES (NEWID(), 'a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d', 'Visa', '4242', '12/28', 'tok_visa_4242_thandiwe', 1, GETDATE());
END;

IF NOT EXISTS (SELECT 1 FROM dbo.PaymentMethods WHERE PatientId = 'b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e')
BEGIN
    INSERT INTO dbo.PaymentMethods (Id, PatientId, CardType, LastFour, ExpiryDate, GatewayToken, IsDefault, CreatedAt)
    VALUES (NEWID(), 'b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e', 'Mastercard', '8812', '09/27', 'tok_mc_8812_jabu', 1, GETDATE());
END;
GO
