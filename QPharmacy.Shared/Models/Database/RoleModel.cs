using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPharmacy.Shared.Models.Database
{
	public class RoleModel
	{
		public int Id { get; set; }

		public string Name { get; set; }

		// Navigation
		public ICollection<UserRoleModel> UserRoles { get; set; }
	}
}
