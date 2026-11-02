using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using QPharmacy.Shared.Models.Database;
using System.Data;

namespace QPharmacy.Repositories
{
	public class AuthRepository : IAuthRepository
	{
		private readonly string _connectionString;

		public AuthRepository(IConfiguration configuration)
		{
			_connectionString = configuration.GetConnectionString("DefaultConnection")
				?? throw new ArgumentNullException("Connection string not found");
		}

		public async Task<Guid> RegisterUserAsync(UserModel user)
		{
			using var connection = new SqlConnection(_connectionString);
			var parameters = new DynamicParameters();
			parameters.Add("@FirstName", user.FirstName);
			parameters.Add("@LastName", user.LastName);
			parameters.Add("@PhoneNumber", user.PhoneNumber);
			parameters.Add("@Email", user.Email);
			parameters.Add("@IdNumber", user.IdNumber);
			parameters.Add("@PasswordHash", user.PasswordHash);
			parameters.Add("@PasswordSalt", user.PasswordSalt);
			parameters.Add("@IsAdmin", user.IsAdmin);
			parameters.Add("@IsDriver", user.IsDriver);
			parameters.Add("@IsDoctor", user.IsDoctor);
			parameters.Add("@IsPharmacist", user.IsPharmacist);
			parameters.Add("@ProfessionalRegistrationNumber", user.ProfessionalRegistrationNumber);
			parameters.Add("@OrganizationOrBranch", user.OrganizationOrBranch);
			parameters.Add("@UserId", dbType: DbType.Guid, direction: ParameterDirection.Output);

			await connection.ExecuteAsync(
				"sp_RegisterUser",
				parameters,
				commandType: CommandType.StoredProcedure
			);

			return parameters.Get<Guid>("@UserId");
		}

		public async Task<(string VerificationId, string OtpCode)> CreateOTPAsync(string phoneNumber, string purpose)
		{
			using var connection = new SqlConnection(_connectionString);
			var parameters = new DynamicParameters();
			parameters.Add("@PhoneNumber", phoneNumber);
			parameters.Add("@Purpose", purpose);
			parameters.Add("@VerificationId", dbType: DbType.String, size: 100, direction: ParameterDirection.Output);
			parameters.Add("@OtpCode", dbType: DbType.String, size: 6, direction: ParameterDirection.Output);

			await connection.ExecuteAsync(
				"sp_CreateOTP",
				parameters,
				commandType: CommandType.StoredProcedure
			);

			return (
				VerificationId: parameters.Get<string>("@VerificationId"),
				OtpCode: parameters.Get<string>("@OtpCode")
			);
		}

		public async Task<bool> VerifyOTPAsync(string verificationId, string otpCode)
		{
			using var connection = new SqlConnection(_connectionString);
			var parameters = new DynamicParameters();
			parameters.Add("@VerificationId", verificationId);
			parameters.Add("@OtpCode", otpCode);

			return await connection.ExecuteScalarAsync<bool>(
				"sp_VerifyOTP",
				parameters,
				commandType: CommandType.StoredProcedure
			);
		}

		public async Task<UserModel?> SignInAsync(string identifier, string passwordHash)
		{
			using var connection = new SqlConnection(_connectionString);
			var parameters = new DynamicParameters();
			parameters.Add("@Identifier", identifier);
			parameters.Add("@PasswordHash", passwordHash);

			var result = await connection.QueryFirstOrDefaultAsync<UserModel>(
				"sp_SignIn",
				parameters,
				commandType: CommandType.StoredProcedure
			);

			return result;
		}

		public async Task<Guid> CreateSessionAsync(Guid userId, string token, string refreshToken, string? deviceInfo, string? ipAddress)
		{
			using var connection = new SqlConnection(_connectionString);
			var parameters = new
			{
				UserId = userId,
				Token = token,
				RefreshToken = refreshToken,
				DeviceInfo = deviceInfo,
				IpAddress = ipAddress,
				ExpiresInMinutes = 1440
			};

			var sessionId = await connection.QueryFirstOrDefaultAsync<Guid?>(
				"sp_CreateSession",
				parameters,
				commandType: CommandType.StoredProcedure
			);

			return sessionId ?? Guid.Empty;
		}

		public async Task<UserModel?> GetUserByIdentifierAsync(string identifier)
		{
			using var connection = new SqlConnection(_connectionString);
			var sql = "SELECT * FROM Users WHERE Email = @Identifier OR PhoneNumber = @Identifier";
			return await connection.QueryFirstOrDefaultAsync<UserModel>(sql, new { Identifier = identifier });
		}

		public async Task<dynamic?> ValidateSessionAsync(string token)
		{
			using var connection = new SqlConnection(_connectionString);
			var parameters = new DynamicParameters();
			parameters.Add("@Token", token);

			var result = await connection.QueryFirstOrDefaultAsync(
				"sp_ValidateSession",
				parameters,
				commandType: CommandType.StoredProcedure
			);

			return result;
		}

		public async Task<bool> RegisterBiometricDeviceAsync(Guid userId, string deviceId, string publicKey, string? deviceName)
		{
			using var connection = new SqlConnection(_connectionString);
			var parameters = new DynamicParameters();
			parameters.Add("@UserId", userId);
			parameters.Add("@DeviceId", deviceId);
			parameters.Add("@PublicKey", publicKey);
			parameters.Add("@DeviceName", deviceName);

			var result = await connection.ExecuteScalarAsync<Guid>(
				"sp_RegisterBiometricDevice",
				parameters,
				commandType: CommandType.StoredProcedure
			);

			return result != Guid.Empty;
		}

		public async Task<UserModel?> ValidateBiometricDeviceAsync(string deviceId)
		{
			using var connection = new SqlConnection(_connectionString);
			var parameters = new DynamicParameters();
			parameters.Add("@DeviceId", deviceId);
			parameters.Add("@UserId", dbType: DbType.Guid, direction: ParameterDirection.Output);

			var result = await connection.QueryFirstOrDefaultAsync<UserModel>(
				"sp_ValidateBiometricDevice",
				parameters,
				commandType: CommandType.StoredProcedure
			);

			return result;
		}

		public async Task<bool> LogoutAsync(string token)
		{
			using var connection = new SqlConnection(_connectionString);
			var parameters = new DynamicParameters();
			parameters.Add("@Token", token);

			var rowsAffected = await connection.ExecuteScalarAsync<int>(
				"sp_Logout",
				parameters,
				commandType: CommandType.StoredProcedure
			);

			return rowsAffected > 0;
		}

		public async Task<(string NewOtpCode, string PhoneNumber, string Purpose)> ResendOTPAsync(string verificationId)
		{
			using var connection = new SqlConnection(_connectionString);
			var parameters = new DynamicParameters();
			parameters.Add("@VerificationId", verificationId);
			parameters.Add("@NewOtpCode", dbType: DbType.String, size: 6, direction: ParameterDirection.Output);

			var result = await connection.QueryFirstOrDefaultAsync(
				"sp_ResendOTP",
				parameters,
				commandType: CommandType.StoredProcedure
			);

			return (
				NewOtpCode: parameters.Get<string>("@NewOtpCode"),
				PhoneNumber: result?.PhoneNumber ?? string.Empty,
				Purpose: result?.Purpose ?? string.Empty
			);
		}

		public async Task<string?> GetPhoneNumberByVerificationIdAsync(string verificationId)
		{
			using var connection = new SqlConnection(_connectionString);
			var sql = "SELECT PhoneNumber FROM OTPVerifications WHERE VerificationId = @VerificationId";
			return await connection.QueryFirstOrDefaultAsync<string>(sql, new { VerificationId = verificationId });
		}

		public async Task<bool> ResetPasswordAsync(string phoneNumber, string passwordHash, string passwordSalt)
		{
			using var connection = new SqlConnection(_connectionString);
			var parameters = new DynamicParameters();
			parameters.Add("@PhoneNumber", phoneNumber);
			parameters.Add("@PasswordHash", passwordHash);
			parameters.Add("@PasswordSalt", passwordSalt);

			var rows = await connection.ExecuteScalarAsync<int>(
				"sp_ResetPassword",
				parameters,
				commandType: CommandType.StoredProcedure
			);
			return rows > 0;
		}
	}
}
