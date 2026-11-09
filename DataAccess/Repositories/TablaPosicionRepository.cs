using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataAccess.Data;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public class TablaPosicionRepository : GenericRepository<TablaPosicion>, ITablaPosicionRepository
    {
        public TablaPosicionRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<TablaPosicion>> GetByTorneoIdAsync(Guid torneoId)
        {
            return await _dbSet
                .AsNoTracking()
                .Where(tp => tp.TorneoId == torneoId)
                .OrderBy(tp => tp.Posicion)
                .ToListAsync();
        }

        public async Task UpsertPosicionesTorneoAsync(Guid torneoId, IEnumerable<TablaPosicion> posiciones)
        {
            var existentes = await _dbSet
                .Where(tp => tp.TorneoId == torneoId)
                .ToListAsync();

            if (existentes.Count > 0)
            {
                _dbSet.RemoveRange(existentes);
            }

            foreach (var pos in posiciones)
            {
                if (pos.Id == Guid.Empty)
                {
                    pos.Id = Guid.NewGuid();
                }
                pos.TorneoId = torneoId;
                pos.UltimaActualizacion = DateTime.UtcNow;
                await _dbSet.AddAsync(pos);
            }

            await _context.SaveChangesAsync();
        }
    }
}

