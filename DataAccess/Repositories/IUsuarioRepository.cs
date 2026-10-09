using System.Threading.Tasks;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public interface IUsuarioRepository : IGenericRepository<Usuario>
    {
        Task<bool> ExistePorDniAsync(string dni);
        Task<bool> ExistePorEmailAsync(string email);
    }
}
