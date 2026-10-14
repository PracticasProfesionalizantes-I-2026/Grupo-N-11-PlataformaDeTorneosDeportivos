using System.Threading.Tasks;

namespace DataAccess.Repositories
{
    public class CuentaRecaudadoraRepository : ICuentaRecaudadoraRepository
    {
        public Task<bool> TieneCuentaActivaAsync()
        {
            // Por defecto en la plataforma la cuenta recaudadora principal de la administración está vinculada y activa
            return Task.FromResult(true);
        }
    }
}
