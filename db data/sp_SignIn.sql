
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
        IsDoctor,
        IsPharmacist,
        IsVerified,
        IsActive,
        CreatedAt,
        OrganizationOrBranch,
        ProfessionalRegistrationNumber
    FROM Users
    WHERE Id = @User;
END