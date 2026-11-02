-- ==============================================================================
-- Tables for Patient Orders & Wellness Marketplace Feature
-- ==============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Orders')
BEGIN
    CREATE TABLE Orders (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        OrderNumber NVARCHAR(50) NOT NULL,
        PatientId UNIQUEIDENTIFIER NOT NULL, -- FK to Users
        OrderDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        TotalAmount DECIMAL(18,2) NOT NULL,
        Status NVARCHAR(50) NOT NULL, -- 'Dispensed', 'In transit', 'Delivered'
        ItemsCount INT NOT NULL,
        DispatchedFrom NVARCHAR(255) NOT NULL
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MarketplaceProducts')
BEGIN
    CREATE TABLE MarketplaceProducts (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Name NVARCHAR(255) NOT NULL,
        Price DECIMAL(18,2) NOT NULL,
        IconName NVARCHAR(50) NOT NULL -- Name of Lucide icon representation
    );
END
GO

-- ==============================================================================
-- Stored Procedures
-- ==============================================================================

-- GET ACTIVE ORDER (For Delivery Tracker)
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetActiveOrder')
    DROP PROCEDURE sp_GetActiveOrder;
GO

CREATE PROCEDURE sp_GetActiveOrder
    @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT TOP 1
        Id,
        OrderNumber,
        PatientId,
        OrderDate,
        TotalAmount,
        Status,
        ItemsCount,
        DispatchedFrom
    FROM Orders
    WHERE PatientId = @PatientId
      AND Status IN ('Dispensed', 'In transit')
    ORDER BY OrderDate DESC;
END
GO

-- GET ORDER HISTORY
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetOrderHistory')
    DROP PROCEDURE sp_GetOrderHistory;
GO

CREATE PROCEDURE sp_GetOrderHistory
    @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        Id,
        OrderNumber,
        PatientId,
        OrderDate,
        TotalAmount,
        Status,
        ItemsCount,
        DispatchedFrom
    FROM Orders
    WHERE PatientId = @PatientId
    ORDER BY OrderDate DESC;
END
GO

-- GET WELLNESS MARKETPLACE PRODUCTS
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetMarketplaceProducts')
    DROP PROCEDURE sp_GetMarketplaceProducts;
GO

CREATE PROCEDURE sp_GetMarketplaceProducts
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        Id,
        Name,
        Price,
        IconName
    FROM MarketplaceProducts;
END
GO

-- SEED ORDER MOCK DATA
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_SeedOrderMockData')
    DROP PROCEDURE sp_SeedOrderMockData;
GO

CREATE PROCEDURE sp_SeedOrderMockData
    @PatientId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    DELETE FROM Orders WHERE PatientId = @PatientId;
    DELETE FROM MarketplaceProducts;
    
    -- Seed active & past orders
    INSERT INTO Orders (OrderNumber, PatientId, OrderDate, TotalAmount, Status, ItemsCount, DispatchedFrom) VALUES
    ('QP-1042', @PatientId, DATEADD(hour, -4, GETUTCDATE()), 486.50, 'In transit', 3, 'Clicks, Claremont'),
    ('QP-1031', @PatientId, DATEADD(day, -29, GETUTCDATE()), 312.00, 'Delivered', 2, 'Clicks, Claremont'),
    ('QP-1019', @PatientId, DATEADD(day, -45, GETUTCDATE()), 640.00, 'Delivered', 4, 'Dis-Chem, Tokai');
    
    -- Seed marketplace products
    INSERT INTO MarketplaceProducts (Name, Price, IconName) VALUES
    ('Vitamin D3 1000IU', 89.00, 'ShoppingBag'),
    ('Digital blood pressure monitor', 649.00, 'Activity');
END
GO
