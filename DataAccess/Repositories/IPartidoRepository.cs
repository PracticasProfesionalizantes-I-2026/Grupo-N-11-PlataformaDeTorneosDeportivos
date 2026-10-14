using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public interface IPartidoRepository : IGenericRepository<Partido>
    {
        Task<IEnumerable<Partido>> GetPartidosByTorneoIdAsync(Guid torneoId);
        Task AddRangeAsync(IEnumerable<Partido> partidos);
    }
}
