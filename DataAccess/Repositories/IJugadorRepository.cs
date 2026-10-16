using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public interface IJugadorRepository : IGenericRepository<Jugador>
    {
        Task<IEnumerable<Jugador>> GetJugadoresByTorneoIdAsync(Guid torneoId);
        Task<Jugador?> GetByIdWithEquipoAndTorneoAsync(Guid id);
    }
}
