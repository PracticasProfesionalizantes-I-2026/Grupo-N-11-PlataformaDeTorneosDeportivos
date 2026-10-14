using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataAccess.Data;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public class PartidoRepository : GenericRepository<Partido>, IPartidoRepository
    {
        public PartidoRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Partido>> GetPartidosByTorneoIdAsync(Guid torneoId)
        {
            return await _dbSet
                .Include(p => p.EquipoLocal)
                .Include(p => p.EquipoVisitante)
                .Where(p => p.TorneoId == torneoId)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task AddRangeAsync(IEnumerable<Partido> partidos)
        {
            await _dbSet.AddRangeAsync(partidos);
            await _context.SaveChangesAsync();
        }
    }
}
