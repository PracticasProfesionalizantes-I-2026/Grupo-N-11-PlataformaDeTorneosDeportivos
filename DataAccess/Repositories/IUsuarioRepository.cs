using System.Threading.Tasks;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public interface IUsuarioRepository : IGenericRepository<Usuario>
    {
        Task<Usuario?> GetByEmailOrDniAsync(string emailOrDni);
        Task<Usuario?> GetByEmailAsync(string email);
        Task<Usuario?> GetByDniAsync(string dni);
        Task<Usuario?> GetByPasswordResetTokenAsync(string token);
    }
}
