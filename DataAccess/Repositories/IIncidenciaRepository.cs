using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public interface IIncidenciaRepository : IGenericRepository<Incidencia>
    {
        Task<IEnumerable<Incidencia>> GetByPartidoIdAsync(Guid partidoId);
        Task<IEnumerable<Incidencia>> GetByPartidoYJugadorAsync(Guid partidoId, Guid jugadorId);
        Task<IEnumerable<Incidencia>> GetByJugadorIdAsync(Guid jugadorId);
        Task<IEnumerable<Incidencia>> GetByJugadorDniAsync(string dni);
    }
}
