using Microsoft.AspNetCore.Identity;

namespace Application.DAL.Models
{
    public class User : IdentityUser
    {
        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string? ProfilePic { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsActive { get; set; }

        //navigation properties 

        //public Employee Employee { get; set; }
    }
}
