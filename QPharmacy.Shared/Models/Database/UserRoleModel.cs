using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPharmacy.Shared.Models.Database
{
	public class UserRoleModel
	{
		public int UserId { get; set; }
		public UserModel User { get; set; }

		public int RoleId { get; set; }
		public RoleModel Role { get; set; }
	}
}
