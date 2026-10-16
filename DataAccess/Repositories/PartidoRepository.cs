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

        public async Task<Partido?> GetByIdWithTorneoAndEquiposAsync(Guid id)
        {
            return await _dbSet
                .Include(p => p.Torneo)
                .Include(p => p.EquipoLocal)
                .Include(p => p.EquipoVisitante)
                .FirstOrDefaultAsync(p => p.Id == id);
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

        public async Task<IEnumerable<Partido>> GetPartidosByVeedorAsync(Guid veedorId, IEnumerable<Guid>? torneoIds = null, DateTime? fecha = null, Guid? torneoId = null)
        {
            var query = _dbSet
                .Include(p => p.EquipoLocal)
                .Include(p => p.EquipoVisitante)
                .Include(p => p.Torneo)
                .AsQueryable();

            var torneosList = torneoIds?.ToList() ?? new List<Guid>();

            if (torneosList.Count > 0)
            {
                query = query.Where(p => p.VeedorId == veedorId || torneosList.Contains(p.TorneoId));
            }
            else
            {
                query = query.Where(p => p.VeedorId == veedorId);
            }

            if (torneoId.HasValue)
            {
                query = query.Where(p => p.TorneoId == torneoId.Value);
            }

            if (fecha.HasValue)
            {
                var targetDate = fecha.Value.Date;
                query = query.Where(p => p.FechaHora.Date == targetDate);
            }

            return await query
                .OrderBy(p => p.FechaHora)
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
