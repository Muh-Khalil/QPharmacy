-- Create Users Table
CREATE TABLE Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    FirstName NVARCHAR(50) NOT NULL,
    LastName NVARCHAR(50) NOT NULL,
    PhoneNumber NVARCHAR(15) NOT NULL UNIQUE,
    Email NVARCHAR(100) NOT NULL UNIQUE,
    IdNumber NVARCHAR(13) NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    PasswordSalt NVARCHAR(255) NOT NULL,
    IsVerified BIT DEFAULT 0,
    IsDriver BIT DEFAULT 0,
    IsAdmin BIT DEFAULT 0,
    IsDoctor BIT DEFAULT 0,
    IsPharmacist BIT DEFAULT 0,
    ProfessionalRegistrationNumber NVARCHAR(100) NULL,
    OrganizationOrBranch NVARCHAR(255) NULL,
    IsActive BIT DEFAULT 1,
    LastLoginDate DATETIME NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    UpdatedAt DATETIME NULL
);
GO

-- Create OTPVerifications Table
CREATE TABLE OTPVerifications (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NULL,
    PhoneNumber NVARCHAR(15) NOT NULL,
    OtpCode NVARCHAR(6) NOT NULL,
    VerificationId NVARCHAR(100) NOT NULL,
    Purpose NVARCHAR(50) NOT NULL, -- 'REGISTRATION', 'LOGIN', 'RESET_PASSWORD'
    ExpiresAt DATETIME NOT NULL,
    IsUsed BIT DEFAULT 0,
    Attempts INT DEFAULT 0,
    CreatedAt DATETIME DEFAULT GETDATE()
);
GO

-- Create UserSessions Table
CREATE TABLE UserSessions (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    Token NVARCHAR(500) NOT NULL,
    RefreshToken NVARCHAR(200) NULL,
    DeviceInfo NVARCHAR(500) NULL,
    IpAddress NVARCHAR(50) NULL,
    ExpiresAt DATETIME NOT NULL,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);
GO

-- Create BiometricDevices Table
CREATE TABLE BiometricDevices (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    DeviceId NVARCHAR(200) NOT NULL,
    PublicKey NVARCHAR(500) NOT NULL,
    DeviceName NVARCHAR(200) NULL,
    IsActive BIT DEFAULT 1,
    RegisteredAt DATETIME DEFAULT GETDATE(),
    LastUsedAt DATETIME NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);
GO

-- STORED PROCEDURES

-- 1. Register User Stored Procedure
CREATE PROCEDURE sp_RegisterUser
    @FirstName NVARCHAR(50),
    @LastName NVARCHAR(50),
    @PhoneNumber NVARCHAR(15),
    @Email NVARCHAR(100),
    @IdNumber NVARCHAR(13) = NULL,
    @PasswordHash NVARCHAR(255),
    @PasswordSalt NVARCHAR(255),
    @IsAdmin BIT = 0,
    @IsDriver BIT = 0,
    @IsDoctor BIT = 0,
    @IsPharmacist BIT = 0,
    @ProfessionalRegistrationNumber NVARCHAR(100) = NULL,
    @OrganizationOrBranch NVARCHAR(255) = NULL,
    @UserId UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Check if user already exists
    IF EXISTS (SELECT 1 FROM Users WHERE PhoneNumber = @PhoneNumber)
    BEGIN
        RAISERROR('Phone number already registered', 16, 1);
        RETURN;
    END
    
    IF EXISTS (SELECT 1 FROM Users WHERE Email = @Email)
    BEGIN
        RAISERROR('Email already registered', 16, 1);
        RETURN;
    END
    
    -- Insert new user
    SET @UserId = NEWID();

    INSERT INTO Users (Id, FirstName, LastName, PhoneNumber, Email, IdNumber, PasswordHash, PasswordSalt, IsAdmin, IsDriver, IsDoctor, IsPharmacist, ProfessionalRegistrationNumber, OrganizationOrBranch)
    VALUES (@UserId, @FirstName, @LastName, @PhoneNumber, @Email, @IdNumber, @PasswordHash, @PasswordSalt, @IsAdmin, @IsDriver, @IsDoctor, @IsPharmacist, @ProfessionalRegistrationNumber, @OrganizationOrBranch);
    
    SELECT @UserId AS UserId;
END
GO

-- 2. Create OTP Stored Procedure
CREATE PROCEDURE sp_CreateOTP
    @PhoneNumber NVARCHAR(15),
    @Purpose NVARCHAR(50),
    @VerificationId NVARCHAR(100) OUTPUT,
    @OtpCode NVARCHAR(6) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    SET @VerificationId = NEWID();
    SET @OtpCode = CAST(FLOOR(RAND() * 900000) + 100000 AS NVARCHAR(6));
    
    -- Get UserId if exists
    DECLARE @UserId UNIQUEIDENTIFIER;
    SELECT @UserId = Id FROM Users WHERE PhoneNumber = @PhoneNumber;
    
    -- Deactivate old OTPs
    UPDATE OTPVerifications 
    SET IsUsed = 1 
    WHERE PhoneNumber = @PhoneNumber AND Purpose = @Purpose AND IsUsed = 0;
    
    -- Insert new OTP
    INSERT INTO OTPVerifications (UserId, PhoneNumber, OtpCode, VerificationId, Purpose, ExpiresAt)
    VALUES (@UserId, @PhoneNumber, @OtpCode, @VerificationId, @Purpose, DATEADD(MINUTE, 10, GETDATE()));
END
GO

-- 3. Verify OTP Stored Procedure
CREATE PROCEDURE sp_VerifyOTP
    @VerificationId NVARCHAR(100),
    @OtpCode NVARCHAR(6)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @IsValid BIT = 0;
    DECLARE @UserId UNIQUEIDENTIFIER;
    DECLARE @PhoneNumber NVARCHAR(15);
    
    -- Check OTP
    SELECT @UserId = UserId, @PhoneNumber = PhoneNumber
    FROM OTPVerifications
    WHERE VerificationId = @VerificationId 
        AND OtpCode = @OtpCode
        AND IsUsed = 0
        AND ExpiresAt > GETDATE()
        AND Attempts < 5;
    
    IF @UserId IS NOT NULL OR (@UserId IS NULL AND @PhoneNumber IS NOT NULL)
    BEGIN
        -- Update OTP as used
        UPDATE OTPVerifications 
        SET IsUsed = 1, 
            Attempts = Attempts + 1
        WHERE VerificationId = @VerificationId;
        
        SET @IsValid = 1;
        
        -- If this is registration OTP, verify the user
        DECLARE @Purpose NVARCHAR(50);
        SELECT @Purpose = Purpose FROM OTPVerifications WHERE VerificationId = @VerificationId;
        
        IF @Purpose = 'REGISTRATION' AND @UserId IS NOT NULL
        BEGIN
            UPDATE Users SET IsVerified = 1, UpdatedAt = GETDATE() WHERE Id = @UserId;
        END
    END
    ELSE
    BEGIN
        -- Increment attempts
        UPDATE OTPVerifications 
        SET Attempts = Attempts + 1
        WHERE VerificationId = @VerificationId;
    END
    
    SELECT @IsValid AS IsValid;
END
GO

-- 4. Sign In Stored Procedure
CREATE PROCEDURE sp_SignIn
    @Identifier NVARCHAR(100),
    @PasswordHash NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @User UNIQUEIDENTIFIER;
    DECLARE @StoredPasswordHash NVARCHAR(255);
    DECLARE @PasswordSalt NVARCHAR(255);
    DECLARE @IsVerified BIT;
    DECLARE @IsActive BIT;
    
    -- Find user by phone or email
    SELECT @User = Id, 
           @StoredPasswordHash = PasswordHash,
           @PasswordSalt = PasswordSalt,
           @IsVerified = IsVerified,
           @IsActive = IsActive
    FROM Users
    WHERE PhoneNumber = @Identifier OR Email = @Identifier;
    
    IF @User IS NULL
    BEGIN
        RAISERROR('User not found', 16, 1);
        RETURN;
    END
    
    IF @IsActive = 0
    BEGIN
        RAISERROR('Account is deactivated', 16, 1);
        RETURN;
    END
    
    IF @IsVerified = 0
    BEGIN
        RAISERROR('Account not verified', 16, 1);
        RETURN;
    END
    
    -- Verify password (this should use HASHBYTES in real implementation)
    IF @StoredPasswordHash != @PasswordHash
    BEGIN
        RAISERROR('Invalid password', 16, 1);
        RETURN;
    END
    
    -- Update last login
    UPDATE Users SET LastLoginDate = GETDATE() WHERE Id = @User;
    
    -- Return user data
    SELECT 
        Id,
        FirstName,
        LastName,
        PhoneNumber,
        Email,
        IsDriver,
        IsAdmin,
        IsVerified,
        IsActive,
        CreatedAt
    FROM Users
    WHERE Id = @User;
END
GO

-- 5. Create Session (JWT Token)
CREATE PROCEDURE sp_CreateSession
    @UserId UNIQUEIDENTIFIER,
    @Token NVARCHAR(500),
    @RefreshToken NVARCHAR(200),
    @DeviceInfo NVARCHAR(500) = NULL,
    @IpAddress NVARCHAR(50) = NULL,
    @ExpiresInMinutes INT = 1440
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Deactivate old sessions for this device if needed
    UPDATE UserSessions 
    SET IsActive = 0 
    WHERE UserId = @UserId AND DeviceInfo = @DeviceInfo AND IsActive = 1;
    
    -- Create new session
    DECLARE @SessionId UNIQUEIDENTIFIER = NEWID();

    INSERT INTO UserSessions (Id, UserId, Token, RefreshToken, DeviceInfo, IpAddress, ExpiresAt)
    VALUES (@SessionId, @UserId, @Token, @RefreshToken, @DeviceInfo, @IpAddress, DATEADD(MINUTE, @ExpiresInMinutes, GETDATE()));
    
    SELECT @SessionId AS SessionId;
END
GO

-- 6. Validate Session
CREATE PROCEDURE sp_ValidateSession
    @Token NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        us.Id AS SessionId,
        us.UserId,
        u.FirstName,
        u.LastName,
        u.Email,
        u.PhoneNumber,
        u.IsDriver,
        u.IsAdmin,
        us.ExpiresAt,
        us.IsActive
    FROM UserSessions us
    INNER JOIN Users u ON us.UserId = u.Id
    WHERE us.Token = @Token 
        AND us.IsActive = 1 
        AND us.ExpiresAt > GETDATE()
        AND u.IsActive = 1;
END
GO

-- 7. Register Biometric Device
CREATE PROCEDURE sp_RegisterBiometricDevice
    @UserId UNIQUEIDENTIFIER,
    @DeviceId NVARCHAR(200),
    @PublicKey NVARCHAR(500),
    @DeviceName NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Check if device already exists
    IF EXISTS (SELECT 1 FROM BiometricDevices WHERE UserId = @UserId AND DeviceId = @DeviceId)
    BEGIN
        UPDATE BiometricDevices 
        SET PublicKey = @PublicKey, 
            DeviceName = @DeviceName,
            IsActive = 1,
            LastUsedAt = GETDATE()
        WHERE UserId = @UserId AND DeviceId = @DeviceId;
        
        SELECT Id AS DeviceId
        FROM BiometricDevices
        WHERE UserId = @UserId AND DeviceId = @DeviceId;
    END
    ELSE
    BEGIN
        DECLARE @BiometricDeviceId UNIQUEIDENTIFIER = NEWID();

        INSERT INTO BiometricDevices (Id, UserId, DeviceId, PublicKey, DeviceName)
        VALUES (@BiometricDeviceId, @UserId, @DeviceId, @PublicKey, @DeviceName);
        
        SELECT @BiometricDeviceId AS DeviceId;
    END
END
GO

-- 8. Validate Biometric Device
CREATE PROCEDURE sp_ValidateBiometricDevice
    @DeviceId NVARCHAR(200),
    @UserId UNIQUEIDENTIFIER = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT @UserId = bd.UserId
    FROM BiometricDevices bd
    INNER JOIN Users u ON bd.UserId = u.Id
    WHERE bd.DeviceId = @DeviceId 
        AND bd.IsActive = 1 
        AND u.IsActive = 1;

    SELECT 
        u.Id,
        u.FirstName,
        u.LastName,
        u.PhoneNumber,
        u.Email,
        u.IsDriver,
        u.IsAdmin,
        u.IsVerified,
        u.IsActive,
        u.CreatedAt
    FROM BiometricDevices bd
    INNER JOIN Users u ON bd.UserId = u.Id
    WHERE bd.DeviceId = @DeviceId 
        AND bd.IsActive = 1 
        AND u.IsActive = 1;
    
    IF @UserId IS NOT NULL
    BEGIN
        UPDATE BiometricDevices SET LastUsedAt = GETDATE() WHERE DeviceId = @DeviceId;
        UPDATE Users SET LastLoginDate = GETDATE() WHERE Id = @UserId;
    END
END
GO

-- 9. Logout (Deactivate Session)
CREATE PROCEDURE sp_Logout
    @Token NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE UserSessions 
    SET IsActive = 0 
    WHERE Token = @Token;
    
    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- 10. Resend OTP
CREATE PROCEDURE sp_ResendOTP
    @VerificationId NVARCHAR(100),
    @NewOtpCode NVARCHAR(6) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @PhoneNumber NVARCHAR(15);
    DECLARE @Purpose NVARCHAR(50);
    
    SELECT @PhoneNumber = PhoneNumber, @Purpose = Purpose
    FROM OTPVerifications
    WHERE VerificationId = @VerificationId AND IsUsed = 0;
    
    IF @PhoneNumber IS NOT NULL
    BEGIN
        -- Generate new OTP
        SET @NewOtpCode = CAST(FLOOR(RAND() * 900000) + 100000 AS NVARCHAR(6));
        
        -- Update existing OTP or create new
        UPDATE OTPVerifications 
        SET OtpCode = @NewOtpCode,
            ExpiresAt = DATEADD(MINUTE, 10, GETDATE()),
            Attempts = 0,
            CreatedAt = GETDATE()
        WHERE VerificationId = @VerificationId;
        
        SELECT @PhoneNumber AS PhoneNumber, @Purpose AS Purpose;
    END
    ELSE
    BEGIN
        RAISERROR('Invalid verification ID', 16, 1);
    END
END
GO

-- 11. Reset Password Stored Procedure
CREATE PROCEDURE sp_ResetPassword
    @PhoneNumber NVARCHAR(15),
    @PasswordHash NVARCHAR(255),
    @PasswordSalt NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE Users
    SET PasswordHash = @PasswordHash,
        PasswordSalt = @PasswordSalt,
        UpdatedAt = GETDATE()
    WHERE PhoneNumber = @PhoneNumber;
    
    SELECT @@ROWCOUNT;
END
GO
