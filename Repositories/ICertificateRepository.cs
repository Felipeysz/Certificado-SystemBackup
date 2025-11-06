using AuthDemo.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthDemo.Repositories
{
    public interface ICertificateRepository
    {
        Task<List<Certificate>> GetAllAsync();
        Task<Certificate?> GetByIdAsync(int id);
        Task AddAsync(Certificate certificate);
        Task UpdateAsync(Certificate certificate);
        Task DeleteAsync(int id);
        Task<bool> ExistsByNomeCursoAsync(string nomeCurso);
    }
}
