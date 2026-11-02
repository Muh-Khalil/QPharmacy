
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