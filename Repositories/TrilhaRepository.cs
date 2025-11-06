// ============================================
// 📁 Repositories/TrilhaRepository.cs
// ============================================
using AuthDemo.Data;
using AuthDemo.Models;
using AuthDemo.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthDemo.Repositories
{
    public class TrilhaRepository : ITrilhaRepository
    {
        private readonly ApplicationDbContext _context;

        public TrilhaRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Trilha>> GetAllAsync()
        {
            return await _context.Trilhas
                .OrderByDescending(t => t.DataCriacao)
                .ToListAsync();
        }

        public async Task<List<Trilha>> GetAtivasAsync()
        {
            return await _context.Trilhas
                .Where(t => t.Ativa)
                .OrderBy(t => t.Nome)
                .ToListAsync();
        }

        public async Task<Trilha?> GetByIdAsync(int id)
        {
            return await _context.Trilhas.FindAsync(id);
        }

        public async Task AddAsync(Trilha trilha)
        {
            _context.Trilhas.Add(trilha);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Trilha trilha)
        {
            _context.Trilhas.Update(trilha);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var trilha = await _context.Trilhas.FindAsync(id);
            if (trilha != null)
            {
                _context.Trilhas.Remove(trilha);
                await _context.SaveChangesAsync();
            }
        }
    }
}