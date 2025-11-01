namespace Application.PL.ViewModels
{
    public class CreateEmployeeRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }           // plain: ensure TLS & strong policy
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Position { get; set; }
    }
}
