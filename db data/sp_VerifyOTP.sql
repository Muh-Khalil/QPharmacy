
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