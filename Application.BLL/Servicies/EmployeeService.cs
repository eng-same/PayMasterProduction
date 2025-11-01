using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application.BLL.Repositories;
using Application.DAL.Models;

namespace Application.BLL.Servicies
{
    public class EmployeeService
    {
        private readonly EmployeeRepository _repository;

        public EmployeeService(EmployeeRepository repository) => _repository = repository;

        public async Task<Employee?> GetByUserIdAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) 
                return null;

            return await _repository.GetByUserIdAsync(userId);
        }
    }
}
