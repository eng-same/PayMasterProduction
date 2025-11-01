using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DAL.Models
{
    public class CompanySupervisor
    {
        public int Id { get; set; }

        // FK to Company
        public int CompanyId { get; set; }
        public Company Company { get; set; }

        // FK to Identity user
        public string UserId { get; set; }
        public User User { get; set; }

        // optional: assigned at
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}
