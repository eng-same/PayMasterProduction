using Microsoft.EntityFrameworkCore;
using Application.DAL.Data;
using Application.DAL.Models;

namespace Application.BLL.Repositories
{
    public class LeaveRequestRepository
    {
        private readonly AppDbContext _db;


        public LeaveRequestRepository(AppDbContext db)
        {
            _db = db;
        }


        public async Task AddAsync(LeaveRequest request)
        {
            await _db.LeaveRequests.AddAsync(request);
            await SaveChangesAsync();
        }


        public async Task<LeaveRequest> GetByIdAsync(int id)
        {
            return await _db.LeaveRequests.Include(l => l.Employee).FirstOrDefaultAsync(l => l.Id == id);
        }


        public async Task<IEnumerable<LeaveRequest>> GetByEmployeeIdAsync(int employeeId)
        {
            return await _db.LeaveRequests
            .Where(l => l.EmployeeId == employeeId)
            .OrderByDescending(l => l.SubmittedAt)
            .ToListAsync();
        }


        public async Task<IEnumerable<LeaveRequest>> GetByCompanyIdAsync(int companyId)
        {
            return await _db.LeaveRequests
            .Include(l => l.Employee)
            .Where(l => l.Employee != null && l.Employee.CompanyId == companyId)
            .OrderByDescending(l => l.SubmittedAt)
            .ToListAsync();
        }

        public async Task UpdateAsync(LeaveRequest request)
        {
            _db.LeaveRequests.Update(request);
            await SaveChangesAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _db.SaveChangesAsync();
        }
    }
}
