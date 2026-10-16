using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public interface IPartidoRepository : IGenericRepository<Partido>
    {
        Task<Partido?> GetByIdWithTorneoAndEquiposAsync(Guid id);
        Task<IEnumerable<Partido>> GetPartidosByTorneoIdAsync(Guid torneoId);
        Task<IEnumerable<Partido>> GetPartidosByVeedorAsync(Guid veedorId, IEnumerable<Guid>? torneoIds = null, DateTime? fecha = null, Guid? torneoId = null);
        Task AddRangeAsync(IEnumerable<Partido> partidos);
    }
}
