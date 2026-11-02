using QPharmacy.Shared.Models.Database;

namespace QPharmacy.Repositories
{
	public interface IAuthRepository
	{
		Task<Guid> RegisterUserAsync(UserModel user);
		Task<(string VerificationId, string OtpCode)> CreateOTPAsync(string phoneNumber, string purpose);
		Task<bool> VerifyOTPAsync(string verificationId, string otpCode);
		Task<UserModel?> SignInAsync(string identifier, string passwordHash);
		Task<UserModel?> GetUserByIdentifierAsync(string identifier);
		Task<Guid> CreateSessionAsync(Guid userId, string token, string refreshToken, string? deviceInfo, string? ipAddress);
		Task<dynamic?> ValidateSessionAsync(string token);
		Task<bool> RegisterBiometricDeviceAsync(Guid userId, string deviceId, string publicKey, string? deviceName);
		Task<UserModel?> ValidateBiometricDeviceAsync(string deviceId);
		Task<bool> LogoutAsync(string token);
		Task<(string NewOtpCode, string PhoneNumber, string Purpose)> ResendOTPAsync(string verificationId);
		Task<string?> GetPhoneNumberByVerificationIdAsync(string verificationId);
		Task<bool> ResetPasswordAsync(string phoneNumber, string passwordHash, string passwordSalt);
	}
}
