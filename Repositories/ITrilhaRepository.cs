// ============================================
// 📁 Repositories/ITrilhaRepository.cs
// ============================================
using AuthDemo.Data;
using AuthDemo.Models;
using AuthDemo.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthDemo.Repositories
{
    public interface ITrilhaRepository
    {
        Task<List<Trilha>> GetAllAsync();
        Task<List<Trilha>> GetAtivasAsync();
        Task<Trilha?> GetByIdAsync(int id);
        Task AddAsync(Trilha trilha);
        Task UpdateAsync(Trilha trilha);
        Task DeleteAsync(int id);
    }
}
