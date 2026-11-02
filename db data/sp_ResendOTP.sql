
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