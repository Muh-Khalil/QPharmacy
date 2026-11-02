
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