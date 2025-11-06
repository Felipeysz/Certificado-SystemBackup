using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthDemo.Repositories
{
    public class CertificateRepository : ICertificateRepository
    {
        private readonly ApplicationDbContext _context;

        public CertificateRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Certificate>> GetAllAsync()
        {
            return await _context.Certificates
                                 .OrderBy(c => c.NomeCurso)
                                 .ToListAsync();
        }

        public async Task<Certificate?> GetByIdAsync(int id)
        {
            return await _context.Certificates
                                 .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task AddAsync(Certificate certificate)
        {
            _context.Certificates.Add(certificate);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Certificate certificate)
        {
            _context.Certificates.Update(certificate);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var certificate = await GetByIdAsync(id);
            if (certificate != null)
            {
                _context.Certificates.Remove(certificate);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsByNomeCursoAsync(string nomeCurso)
        {
            return await _context.Certificates.AnyAsync(c => c.NomeCurso == nomeCurso);
        }
    }
}
