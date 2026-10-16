using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataAccess.Data;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public class JugadorRepository : GenericRepository<Jugador>, IJugadorRepository
    {
        public JugadorRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Jugador>> GetJugadoresByTorneoIdAsync(Guid torneoId)
        {
            return await _context.Jugadores
                .Include(j => j.Equipo)
                .Where(j => j.Equipo.TorneoId == torneoId)
                .ToListAsync();
        }

        public async Task<Jugador?> GetByIdWithEquipoAndTorneoAsync(Guid id)
        {
            return await _dbSet
                .Include(j => j.Equipo)
                    .ThenInclude(e => e.Torneo)
                .FirstOrDefaultAsync(j => j.Id == id);
        }
    }
}
