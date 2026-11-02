CREATE PROCEDURE sp_ValidateBiometricDevice
    @DeviceId NVARCHAR(200),
    @UserId UNIQUEIDENTIFIER = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- First assign variable
    SELECT @UserId = bd.UserId
    FROM BiometricDevices bd
    INNER JOIN Users u ON bd.UserId = u.Id
    WHERE bd.DeviceId = @DeviceId
      AND bd.IsActive = 1
      AND u.IsActive = 1;

    -- Then return data
    SELECT
        u.Id,
        u.FirstName,
        u.LastName,
        u.PhoneNumber,
        u.Email,
        u.IsDriver,
        u.IsAdmin,
        u.IsVerified,
        u.IsActive,
        u.CreatedAt
    FROM BiometricDevices bd
    INNER JOIN Users u ON bd.UserId = u.Id
    WHERE bd.DeviceId = @DeviceId
      AND bd.IsActive = 1
      AND u.IsActive = 1;

    IF @UserId IS NOT NULL
    BEGIN
        UPDATE BiometricDevices
        SET LastUsedAt = GETDATE()
        WHERE DeviceId = @DeviceId;

        UPDATE Users
        SET LastLoginDate = GETDATE()
        WHERE Id = @UserId;
    END
END
