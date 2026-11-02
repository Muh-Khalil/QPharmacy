
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