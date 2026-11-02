using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPharmacy.Shared.Models.Database
{
	public class OTPSession
	{
		public Guid Id { get; set; }
		public Guid? UserId { get; set; }
		public string PhoneNumber { get; set; } = string.Empty;
		public string OtpCode { get; set; } = string.Empty;
		public string VerificationId { get; set; } = string.Empty;
		public string Purpose { get; set; } = string.Empty;
		public DateTime ExpiresAt { get; set; }
		public bool IsUsed { get; set; }
		public int Attempts { get; set; }
	}
}
