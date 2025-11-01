using Microsoft.EntityFrameworkCore;
using Application.DAL.Data;
using Application.DAL.Models;

namespace Application.BLL.Repositories
{
    public class EmployeeRepository
    {
        private readonly AppDbContext _db;
        public EmployeeRepository(AppDbContext db) => _db = db;

        public async Task<Employee> GetByUserIdAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return null; //thinl about it 
            return await _db.Employees
                            .Include(e => e.Company)
                            .FirstOrDefaultAsync(e => e.UserId == userId);
        }

        public async Task<Employee> GetByIdAsync(int id)
        {
            return await _db.Employees
                            .Include(e => e.Company)
                            .FirstOrDefaultAsync(e => e.Id == id);
        }
    }
}
