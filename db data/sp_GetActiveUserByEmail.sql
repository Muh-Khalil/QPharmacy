CREATE PROCEDURE sp_GetActiveUserByEmail
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        Id,
        Email,
        PhoneNumber,
        PasswordHash,
        IsActive,
        CreatedAt
    FROM Users
    WHERE Email = @Email
      AND IsActive = 1
END