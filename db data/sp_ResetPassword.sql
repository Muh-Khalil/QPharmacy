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
