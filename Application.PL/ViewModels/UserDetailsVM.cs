using Application.DAL.Models;

namespace Application.PL.ViewModels
{
    public class UserDetailsVM
    {
        public User User { get; set; } = null!;
        public List<string> Roles { get; set; } = new();
        public Employee? Employee { get; set; }
        public CompanySupervisor? CompanySupervisor { get; set; }
    }
}
