using System.Threading.Tasks;

namespace BusinessLogic.Services
{
    public interface ICuentaRecaudadoraService
    {
        Task<bool> IsCuentaActivaAsync();
    }

    public class CuentaRecaudadoraService : ICuentaRecaudadoraService
    {
        public Task<bool> IsCuentaActivaAsync()
        {
            return Task.FromResult(true);
        }
    }
}
