using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QPharmacy.Shared.Models.Database;

namespace QPharmacy.Repositories
{
	public class MockAuthRepository : IAuthRepository
	{
		private static readonly List<UserModel> _users = new();
		private static readonly List<OtpRecord> _otps = new();
		private static readonly List<SessionRecord> _sessions = new();
		private static readonly List<BiometricRecord> _biometrics = new();

		// Add a static constructor to seed some initial test users
		static MockAuthRepository()
		{
			// Seed a test driver (Password is 'password123')
			var driverSaltBytes = System.Text.Encoding.UTF8.GetBytes("salt123");
			using var hmacDriver = new System.Security.Cryptography.HMACSHA512(driverSaltBytes);
			var driverHash = Convert.ToBase64String(hmacDriver.ComputeHash(System.Text.Encoding.UTF8.GetBytes("password123")));

			_users.Add(new UserModel
			{
				Id = Guid.NewGuid(),
				FirstName = "John",
				LastName = "Driver",
				Email = "driver@qpharmacy.co.za",
				PhoneNumber = "0821111111",
				PasswordHash = driverHash,
				PasswordSalt = Convert.ToBase64String(driverSaltBytes),
				IsVerified = true,
				IsDriver = true,
				IsAdmin = false,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			});

			// Seed a test admin (Password is 'admin123')
			var adminSaltBytes = System.Text.Encoding.UTF8.GetBytes("salt456");
			using var hmacAdmin = new System.Security.Cryptography.HMACSHA512(adminSaltBytes);
			var adminHash = Convert.ToBase64String(hmacAdmin.ComputeHash(System.Text.Encoding.UTF8.GetBytes("admin123")));

			_users.Add(new UserModel
			{
				Id = Guid.NewGuid(),
				FirstName = "Sarah",
				LastName = "Admin",
				Email = "admin@qpharmacy.co.za",
				PhoneNumber = "0822222222",
				PasswordHash = adminHash,
				PasswordSalt = Convert.ToBase64String(adminSaltBytes),
				IsVerified = true,
				IsDriver = false,
				IsAdmin = true,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			});
		}

		public Task<Guid> RegisterUserAsync(UserModel user)
		{
			lock (_users)
			{
				if (_users.Any(u => u.PhoneNumber == user.PhoneNumber))
				{
					throw new Exception("Phone number already registered");
				}

				if (_users.Any(u => u.Email == user.Email))
				{
					throw new Exception("Email already registered");
				}

				user.Id = Guid.NewGuid();
				user.CreatedAt = DateTime.UtcNow;
				user.IsVerified = false; // Requires OTP
				user.IsActive = true;
				_users.Add(user);

				return Task.FromResult(user.Id);
			}
		}

		public Task<(string VerificationId, string OtpCode)> CreateOTPAsync(string phoneNumber, string purpose)
		{
			lock (_otps)
			{
				// Deactivate old OTPs for this phone/purpose
				foreach (var oldOtp in _otps.Where(o => o.PhoneNumber == phoneNumber && o.Purpose == purpose && !o.IsUsed))
				{
					oldOtp.IsUsed = true;
				}

				var verificationId = Guid.NewGuid().ToString();
				// Use 123456 as the mock code so it's always easy to sign up/verify
				var otpCode = "123456";

				_otps.Add(new OtpRecord
				{
					VerificationId = verificationId,
					PhoneNumber = phoneNumber,
					OtpCode = otpCode,
					Purpose = purpose,
					ExpiresAt = DateTime.UtcNow.AddMinutes(10),
					IsUsed = false,
					Attempts = 0
				});

				return Task.FromResult((verificationId, otpCode));
			}
		}

		public Task<bool> VerifyOTPAsync(string verificationId, string otpCode)
		{
			lock (_otps)
			{
				var record = _otps.FirstOrDefault(o => o.VerificationId == verificationId && o.OtpCode == otpCode && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow);

				if (record != null)
				{
					record.IsUsed = true;

					if (record.Purpose == "REGISTRATION")
					{
						lock (_users)
						{
							var user = _users.FirstOrDefault(u => u.PhoneNumber == record.PhoneNumber);
							if (user != null)
							{
								user.IsVerified = true;
								user.UpdatedAt = DateTime.UtcNow;
							}
						}
					}
					return Task.FromResult(true);
				}

				// Increment attempts
				var failedRecord = _otps.FirstOrDefault(o => o.VerificationId == verificationId);
				if (failedRecord != null)
				{
					failedRecord.Attempts++;
				}

				return Task.FromResult(false);
			}
		}

		public Task<UserModel?> SignInAsync(string identifier, string passwordHash)
		{
			lock (_users)
			{
				var user = _users.FirstOrDefault(u => u.Email == identifier || u.PhoneNumber == identifier);

				if (user == null)
				{
					throw new Exception("User not found");
				}

				if (!user.IsActive)
				{
					throw new Exception("Account is deactivated");
				}

				if (!user.IsVerified)
				{
					throw new Exception("Account not verified");
				}

				if (user.PasswordHash != passwordHash)
				{
					throw new Exception("Invalid password");
				}

				user.LastLoginDate = DateTime.UtcNow;
				return Task.FromResult<UserModel?>(user);
			}
		}

		public Task<UserModel?> GetUserByIdentifierAsync(string identifier)
		{
			lock (_users)
			{
				var user = _users.FirstOrDefault(u => u.Email == identifier || u.PhoneNumber == identifier);
				return Task.FromResult<UserModel?>(user);
			}
		}

		public Task<Guid> CreateSessionAsync(Guid userId, string token, string refreshToken, string? deviceInfo, string? ipAddress)
		{
			lock (_sessions)
			{
				// Deactivate old active sessions
				foreach (var oldSession in _sessions.Where(s => s.UserId == userId && s.DeviceInfo == deviceInfo && s.IsActive))
				{
					oldSession.IsActive = false;
				}

				var sessionId = Guid.NewGuid();
				_sessions.Add(new SessionRecord
				{
					Id = sessionId,
					UserId = userId,
					Token = token,
					RefreshToken = refreshToken,
					DeviceInfo = deviceInfo ?? string.Empty,
					IpAddress = ipAddress ?? string.Empty,
					ExpiresAt = DateTime.UtcNow.AddMinutes(1440),
					IsActive = true
				});

				return Task.FromResult(sessionId);
			}
		}

		public Task<dynamic?> ValidateSessionAsync(string token)
		{
			lock (_sessions)
			{
				var session = _sessions.FirstOrDefault(s => s.Token == token && s.IsActive && s.ExpiresAt > DateTime.UtcNow);
				if (session == null)
				{
					return Task.FromResult<dynamic?>(null);
				}

				lock (_users)
				{
					var user = _users.FirstOrDefault(u => u.Id == session.UserId && u.IsActive);
					if (user == null)
					{
						return Task.FromResult<dynamic?>(null);
					}

					// Return as dynamic matching the expected shape in sp_ValidateSession
					dynamic result = new System.Dynamic.ExpandoObject();
					var dict = (IDictionary<string, object>)result;
					dict["SessionId"] = session.Id;
					dict["UserId"] = user.Id;
					dict["FirstName"] = user.FirstName;
					dict["LastName"] = user.LastName;
					dict["Email"] = user.Email;
					dict["PhoneNumber"] = user.PhoneNumber;
					dict["IsDriver"] = user.IsDriver;
					dict["IsAdmin"] = user.IsAdmin;
					dict["ExpiresAt"] = session.ExpiresAt;
					dict["IsActive"] = session.IsActive;

					return Task.FromResult<dynamic?>(result);
				}
			}
		}

		public Task<bool> RegisterBiometricDeviceAsync(Guid userId, string deviceId, string publicKey, string? deviceName)
		{
			lock (_biometrics)
			{
				var record = _biometrics.FirstOrDefault(b => b.UserId == userId && b.DeviceId == deviceId);
				if (record != null)
				{
					record.PublicKey = publicKey;
					record.DeviceName = deviceName ?? string.Empty;
					record.IsActive = true;
				}
				else
				{
					_biometrics.Add(new BiometricRecord
					{
						UserId = userId,
						DeviceId = deviceId,
						PublicKey = publicKey,
						DeviceName = deviceName ?? string.Empty,
						IsActive = true
					});
				}
				return Task.FromResult(true);
			}
		}

		public Task<UserModel?> ValidateBiometricDeviceAsync(string deviceId)
		{
			lock (_biometrics)
			{
				var record = _biometrics.FirstOrDefault(b => b.DeviceId == deviceId && b.IsActive);
				if (record == null)
				{
					return Task.FromResult<UserModel?>(null);
				}

				lock (_users)
				{
					var user = _users.FirstOrDefault(u => u.Id == record.UserId && u.IsActive);
					if (user != null)
					{
						user.LastLoginDate = DateTime.UtcNow;
					}
					return Task.FromResult<UserModel?>(user);
				}
			}
		}

		public Task<bool> LogoutAsync(string token)
		{
			lock (_sessions)
			{
				var session = _sessions.FirstOrDefault(s => s.Token == token);
				if (session != null)
				{
					session.IsActive = false;
					return Task.FromResult(true);
				}
				return Task.FromResult(false);
			}
		}

		public Task<(string NewOtpCode, string PhoneNumber, string Purpose)> ResendOTPAsync(string verificationId)
		{
			lock (_otps)
			{
				var record = _otps.FirstOrDefault(o => o.VerificationId == verificationId && !o.IsUsed);
				if (record != null)
				{
					record.OtpCode = "123456"; // Always return 123456 for ease
					record.ExpiresAt = DateTime.UtcNow.AddMinutes(10);
					record.Attempts = 0;

					return Task.FromResult((record.OtpCode, record.PhoneNumber, record.Purpose));
				}

				throw new Exception("Invalid verification ID");
			}
		}

		public Task<string?> GetPhoneNumberByVerificationIdAsync(string verificationId)
		{
			lock (_otps)
			{
				var record = _otps.FirstOrDefault(o => o.VerificationId == verificationId);
				return Task.FromResult<string?>(record?.PhoneNumber);
			}
		}

		public Task<bool> ResetPasswordAsync(string phoneNumber, string passwordHash, string passwordSalt)
		{
			lock (_users)
			{
				var user = _users.FirstOrDefault(u => u.PhoneNumber == phoneNumber);
				if (user != null)
				{
					user.PasswordHash = passwordHash;
					user.PasswordSalt = passwordSalt;
					user.UpdatedAt = DateTime.UtcNow;
					return Task.FromResult(true);
				}
				return Task.FromResult(false);
			}
		}

		private class OtpRecord
		{
			public string VerificationId { get; set; } = string.Empty;
			public string PhoneNumber { get; set; } = string.Empty;
			public string OtpCode { get; set; } = string.Empty;
			public string Purpose { get; set; } = string.Empty;
			public DateTime ExpiresAt { get; set; }
			public bool IsUsed { get; set; }
			public int Attempts { get; set; }
		}

		private class SessionRecord
		{
			public Guid Id { get; set; }
			public Guid UserId { get; set; }
			public string Token { get; set; } = string.Empty;
			public string RefreshToken { get; set; } = string.Empty;
			public string DeviceInfo { get; set; } = string.Empty;
			public string IpAddress { get; set; } = string.Empty;
			public DateTime ExpiresAt { get; set; }
			public bool IsActive { get; set; }
		}

		private class BiometricRecord
		{
			public Guid UserId { get; set; }
			public string DeviceId { get; set; } = string.Empty;
			public string PublicKey { get; set; } = string.Empty;
			public string DeviceName { get; set; } = string.Empty;
			public bool IsActive { get; set; }
		}
	}
}
