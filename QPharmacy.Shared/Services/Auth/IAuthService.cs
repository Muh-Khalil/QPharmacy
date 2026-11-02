using QPharmacy.Shared.Models.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPharmacy.Shared.Service
{
	public interface IAuthService
	{
		Task<AuthResult> SignInAsync(SignInModel model);
		Task<AuthResult> RegisterAsync(SignUpModel model);
		Task<AuthResult> VerifyOtpAsync(string verificationId, string otpCode);
		Task<AuthResult> BiometricSignInAsync();
		Task<bool> ResendOtpAsync(string verificationId);
		Task LogoutAsync(string? token = null);
		Task<AuthResult> RequestPasswordResetAsync(string identifier);
		Task<AuthResult> CompletePasswordResetAsync(string verificationId, string otpCode, string newPassword);
	}
}
