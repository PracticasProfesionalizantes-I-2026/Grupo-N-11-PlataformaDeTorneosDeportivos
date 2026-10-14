using System.Threading.Tasks;

namespace DataAccess.Repositories
{
    public interface ICuentaRecaudadoraRepository
    {
        Task<bool> TieneCuentaActivaAsync();
    }
}
