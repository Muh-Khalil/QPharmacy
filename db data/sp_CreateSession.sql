
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
