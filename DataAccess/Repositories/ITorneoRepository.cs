using System.Threading.Tasks;
using DataAccess.Entities;
using System.Collections.Generic;

namespace DataAccess.Repositories
{
    public interface ITorneoRepository : IGenericRepository<Torneo>
    {
        Task<IEnumerable<Torneo>> GetTorneosConEquiposAsync();
    }
}
