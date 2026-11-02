using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using QPharmacy.Repositories;
using QPharmacy.Shared.Models.Auth;
using QPharmacy.Shared.Service;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using QPharmacy.Shared.Models.Database;

namespace QPharmacy.Services
{
	public class AuthService : IAuthService
	{
		private readonly IAuthRepository _authRepository;
		private readonly IConfiguration _configuration;
		private readonly IClientInfoProvider _clientInfoProvider;

		public AuthService(
			IAuthRepository authRepository,
			IConfiguration configuration,
			IClientInfoProvider clientInfoProvider)
		{
			_authRepository = authRepository;
			_configuration = configuration;
			_clientInfoProvider = clientInfoProvider;
		}

		public async Task<AuthResult> SignInAsync(SignInModel model)
		{
			try
			{
				// Retrieve user to read their password salt
				var userRecord = await _authRepository.GetUserByIdentifierAsync(model.Identifier);
				if (userRecord == null)
				{
					return new AuthResult
					{
						Success = false,
						ErrorMessage = "Invalid credentials"
					};
				}

				// Compute the hash of the input password using the retrieved salt
				string passwordHash;
				if (!string.IsNullOrEmpty(userRecord.PasswordSalt))
				{
					var saltBytes = Convert.FromBase64String(userRecord.PasswordSalt);
					using var hmac = new HMACSHA512(saltBytes);
					passwordHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(model.Password)));
				}
				else
				{
					passwordHash = HashPassword(model.Password);
				}

				var user = await _authRepository.SignInAsync(model.Identifier, passwordHash);

				if (user == null)
				{
					return new AuthResult
					{
						Success = false,
						ErrorMessage = "Invalid credentials"
					};
				}

				// Generate JWT Token
				var token = GenerateJwtToken(user);
				var refreshToken = GenerateRefreshToken();

				// Get device info and IP
				var deviceInfo = _clientInfoProvider.GetUserAgent();
				var ipAddress = _clientInfoProvider.GetIpAddress();

				// Create session
				await _authRepository.CreateSessionAsync(user.Id, token, refreshToken, deviceInfo, ipAddress);

				return new AuthResult
				{
					Success = true,
					Token = token,
					User = MapToUserModel(user)
				};
			}
			catch (Exception ex)
			{
				return new AuthResult
				{
					Success = false,
					ErrorMessage = ex.Message
				};
			}
		}

		public async Task<AuthResult> RegisterAsync(SignUpModel model)
		{
			try
			{
				// Hash password
				var (hash, salt) = HashPasswordWithSalt(model.Password);

				bool isAdmin = model.Role == "Super Admin" || model.Role == "Admin";
				bool isDriver = model.Role == "Driver";
				bool isDoctor = model.Role == "Doctor";
				bool isPharmacist = model.Role == "Pharmacist";

				var user = new UserModel
				{
					FirstName = model.FirstName,
					LastName = model.LastName,
					PhoneNumber = model.PhoneNumber,
					Email = model.Email,
					IdNumber = model.IdNumber,
					PasswordHash = hash,
					PasswordSalt = salt,
					IsVerified = false,
					IsDriver = isDriver,
					IsAdmin = isAdmin,
					IsDoctor = isDoctor,
					IsPharmacist = isPharmacist,
					ProfessionalRegistrationNumber = model.ProfessionalRegistrationNumber ?? string.Empty,
					OrganizationOrBranch = model.OrganizationOrBranch ?? string.Empty,
					IsActive = true
				};

				var userId = await _authRepository.RegisterUserAsync(user);

				// Create OTP for verification
				var (verificationId, otpCode) = await _authRepository.CreateOTPAsync(
					model.PhoneNumber,
					"REGISTRATION"
				);

				// In production, send SMS here
				await SendOTPSms(model.PhoneNumber, otpCode);

				return new AuthResult
				{
					Success = true,
					VerificationId = verificationId,
					User = MapToUserModel(user)
				};
			}
			catch (Exception ex)
			{
				return new AuthResult
				{
					Success = false,
					ErrorMessage = ex.Message
				};
			}
		}

		public async Task<AuthResult> VerifyOtpAsync(string verificationId, string otpCode)
		{
			try
			{
				var isValid = await _authRepository.VerifyOTPAsync(verificationId, otpCode);

				if (!isValid)
				{
					return new AuthResult
					{
						Success = false,
						ErrorMessage = "Invalid or expired OTP"
					};
				}

				return new AuthResult
				{
					Success = true
				};
			}
			catch (Exception ex)
			{
				return new AuthResult
				{
					Success = false,
					ErrorMessage = ex.Message
				};
			}
		}

		public async Task<AuthResult> BiometricSignInAsync()
		{
			try
			{
				// Get device ID from client
				var deviceId = await GetDeviceId();

				var user = await _authRepository.ValidateBiometricDeviceAsync(deviceId);

				if (user == null)
				{
					return new AuthResult
					{
						Success = false,
						ErrorMessage = "Biometric authentication failed"
					};
				}

				var token = GenerateJwtToken(user);
				var refreshToken = GenerateRefreshToken();

				return new AuthResult
				{
					Success = true,
					Token = token,
					User = MapToUserModel(user)
				};
			}
			catch (Exception ex)
			{
				return new AuthResult
				{
					Success = false,
					ErrorMessage = ex.Message
				};
			}
		}

		public async Task<bool> ResendOtpAsync(string verificationId)
		{
			try
			{
				var (newOtpCode, phoneNumber, purpose) = await _authRepository.ResendOTPAsync(verificationId);

				if (!string.IsNullOrEmpty(phoneNumber))
				{
					await SendOTPSms(phoneNumber, newOtpCode);
					return true;
				}

				return false;
			}
			catch
			{
				return false;
			}
		}

		public async Task LogoutAsync(string? token = null)
		{
			token ??= GetCurrentToken();
			if (!string.IsNullOrEmpty(token))
			{
				await _authRepository.LogoutAsync(token);
			}
		}

		// Helper Methods
		private string HashPassword(string password)
		{
			using var sha256 = SHA256.Create();
			var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
			return Convert.ToBase64String(hashedBytes);
		}

		private (string Hash, string Salt) HashPasswordWithSalt(string password)
		{
			using var hmac = new HMACSHA512();
			var salt = Convert.ToBase64String(hmac.Key);
			var hash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
			return (hash, salt);
		}

		private string GenerateJwtToken(Shared.Models.Database.UserModel user)
		{
			var securityKey = new SymmetricSecurityKey(
				Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "your-secret-key-here-at-least-32-characters-long")
			);
			var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

			var role = GetRole(user);
			var claims = new[]
			{
				new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
				new Claim(JwtRegisteredClaimNames.Email, user.Email),
				new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName),
				new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName),
				new Claim("PhoneNumber", user.PhoneNumber),
				new Claim("IsDriver", user.IsDriver.ToString()),
				new Claim("IsAdmin", user.IsAdmin.ToString()),
				new Claim(ClaimTypes.Role, role),
				new Claim("organization", user.OrganizationOrBranch ?? string.Empty),
				new Claim("professional_registration", user.ProfessionalRegistrationNumber ?? string.Empty),
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
			};

			var token = new JwtSecurityToken(
				issuer: _configuration["Jwt:Issuer"],
				audience: _configuration["Jwt:Audience"],
				claims: claims,
				expires: DateTime.UtcNow.AddDays(7),
				signingCredentials: credentials
			);

			return new JwtSecurityTokenHandler().WriteToken(token);
		}

		private string GenerateRefreshToken()
		{
			var randomNumber = new byte[32];
			using var rng = RandomNumberGenerator.Create();
			rng.GetBytes(randomNumber);
			return Convert.ToBase64String(randomNumber);
		}

		private async Task SendOTPSms(string phoneNumber, string otpCode)
		{
			// Implement SMS sending logic (e.g., using Twilio, Clickatell, or local provider)
			// For MVP, just log it
			Console.WriteLine($"Sending OTP {otpCode} to {phoneNumber}");
			System.Diagnostics.Debug.WriteLine($"Sending OTP {otpCode} to {phoneNumber}");

			try
			{
				// Log the OTP code to a file in the workspace root for easy access during development/testing
				string workspaceOtpPath = @"D:\Dev\QPharmacy\otp_code.txt";
				await File.WriteAllTextAsync(workspaceOtpPath, $"OTP Code: {otpCode}\nPhone: {phoneNumber}\nTimestamp: {DateTime.Now}");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to write OTP to file: {ex.Message}");
			}
		}

		private async Task<string> GetDeviceId()
		{
			// In MAUI, get device ID from native layer
			// For Blazor, use localStorage or browser fingerprint
			return await Task.FromResult(Guid.NewGuid().ToString());
		}

		private string GetCurrentToken()
		{
			return _clientInfoProvider.GetToken();
		}

		private UserModel MapToUserModel(UserModel user)
		{
			if (user == null)
				return null;

			return new UserModel
			{
				Id = user.Id,
				FirstName = user.FirstName,
				LastName = user.LastName,
				PhoneNumber = user.PhoneNumber,
				Email = user.Email,
				IsDriver = user.IsDriver,
				IsAdmin = user.IsAdmin,
				IsDoctor = user.IsDoctor,
				IsPharmacist = user.IsPharmacist,
				ProfessionalRegistrationNumber = user.ProfessionalRegistrationNumber,
				OrganizationOrBranch = user.OrganizationOrBranch,
				CreatedAt = user.CreatedAt
			};
		}

		private static string GetRole(UserModel user)
		{
			if (user.IsAdmin) return "Admin";
			if (user.IsPharmacist) return "Pharmacist";
			if (user.IsDoctor) return "Doctor";
			if (user.IsDriver) return "Driver";
			return "Patient";
		}

		public async Task<AuthResult> RequestPasswordResetAsync(string identifier)
		{
			try
			{
				var user = await _authRepository.GetUserByIdentifierAsync(identifier);
				if (user == null)
				{
					return new AuthResult
					{
						Success = false,
						ErrorMessage = "User not found"
					};
				}

				// Create OTP for password reset
				var (verificationId, otpCode) = await _authRepository.CreateOTPAsync(
					user.PhoneNumber,
					"RESET_PASSWORD"
				);

				// Log/Send OTP SMS
				await SendOTPSms(user.PhoneNumber, otpCode);

				return new AuthResult
				{
					Success = true,
					VerificationId = verificationId
				};
			}
			catch (Exception ex)
			{
				return new AuthResult
				{
					Success = false,
					ErrorMessage = ex.Message
				};
			}
		}

		public async Task<AuthResult> CompletePasswordResetAsync(string verificationId, string otpCode, string newPassword)
		{
			try
			{
				// 1. Verify OTP
				var isValid = await _authRepository.VerifyOTPAsync(verificationId, otpCode);
				if (!isValid)
				{
					return new AuthResult
					{
						Success = false,
						ErrorMessage = "Invalid or expired OTP"
					};
				}

				// 2. Retrieve phone number associated with this verification ID
				var phoneNumber = await _authRepository.GetPhoneNumberByVerificationIdAsync(verificationId);
				if (string.IsNullOrEmpty(phoneNumber))
				{
					return new AuthResult
					{
						Success = false,
						ErrorMessage = "Verification record not found"
					};
				}

				// 3. Hash the new password with salt
				var (hash, salt) = HashPasswordWithSalt(newPassword);

				// 4. Update the password in database
				var result = await _authRepository.ResetPasswordAsync(phoneNumber, hash, salt);
				if (!result)
				{
					return new AuthResult
					{
						Success = false,
						ErrorMessage = "Failed to reset password"
					};
				}

				return new AuthResult
				{
					Success = true
				};
			}
			catch (Exception ex)
			{
				return new AuthResult
				{
					Success = false,
					ErrorMessage = ex.Message
				};
			}
		}
	}
}
