using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public interface IEquipoRepository : IGenericRepository<Equipo>
    {
        Task<bool> ExistsNombreInTorneoAsync(Guid torneoId, string nombre);
        Task<IEnumerable<Equipo>> GetByTorneoIdWithJugadoresAsync(Guid torneoId);
    }
}
