
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
