using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public interface ITablaPosicionRepository : IGenericRepository<TablaPosicion>
    {
        Task<IEnumerable<TablaPosicion>> GetByTorneoIdAsync(Guid torneoId);
        Task UpsertPosicionesTorneoAsync(Guid torneoId, IEnumerable<TablaPosicion> posiciones);
    }
}

