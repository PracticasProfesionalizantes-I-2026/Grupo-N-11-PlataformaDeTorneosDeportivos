using System.Threading.Tasks;

namespace DataAccess.Repositories
{
    public interface IDisciplinaRepository
    {
        Task<bool> ExistsAndIsActiveAsync(int disciplinaId);
    }
}
