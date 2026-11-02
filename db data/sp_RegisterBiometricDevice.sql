
-- 7. Register Biometric Device
CREATE PROCEDURE sp_RegisterBiometricDevice
    @UserId UNIQUEIDENTIFIER,
    @DeviceId NVARCHAR(200),
    @PublicKey NVARCHAR(500),
    @DeviceName NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Check if device already exists
    IF EXISTS (SELECT 1 FROM BiometricDevices WHERE UserId = @UserId AND DeviceId = @DeviceId)
    BEGIN
        UPDATE BiometricDevices 
        SET PublicKey = @PublicKey, 
            DeviceName = @DeviceName,
            IsActive = 1,
            LastUsedAt = GETDATE()
        WHERE UserId = @UserId AND DeviceId = @DeviceId;
        
        SELECT Id AS DeviceId
        FROM BiometricDevices
        WHERE UserId = @UserId AND DeviceId = @DeviceId;
    END
    ELSE
    BEGIN
        DECLARE @BiometricDeviceId UNIQUEIDENTIFIER = NEWID();

        INSERT INTO BiometricDevices (Id, UserId, DeviceId, PublicKey, DeviceName)
        VALUES (@BiometricDeviceId, @UserId, @DeviceId, @PublicKey, @DeviceName);
        
        SELECT @BiometricDeviceId AS DeviceId;
    END
END
