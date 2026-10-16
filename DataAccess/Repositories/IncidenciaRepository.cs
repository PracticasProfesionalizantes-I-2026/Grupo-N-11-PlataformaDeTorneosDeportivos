using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataAccess.Data;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public class IncidenciaRepository : GenericRepository<Incidencia>, IIncidenciaRepository
    {
        public IncidenciaRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Incidencia>> GetByPartidoIdAsync(Guid partidoId)
        {
            return await _dbSet
                .Include(i => i.Equipo)
                .Include(i => i.Jugador)
                .Where(i => i.PartidoId == partidoId)
                .OrderBy(i => i.Minuto)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<Incidencia>> GetByPartidoYJugadorAsync(Guid partidoId, Guid jugadorId)
        {
            return await _dbSet
                .Where(i => i.PartidoId == partidoId && i.JugadorId == jugadorId)
                .OrderBy(i => i.Minuto)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<Incidencia>> GetByJugadorIdAsync(Guid jugadorId)
        {
            return await _dbSet
                .Include(i => i.Partido)
                    .ThenInclude(p => p.Torneo)
                .Include(i => i.Equipo)
                .Where(i => i.JugadorId == jugadorId)
                .OrderBy(i => i.FechaRegistro)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<Incidencia>> GetByJugadorDniAsync(string dni)
        {
            return await _dbSet
                .Include(i => i.Jugador)
                .Include(i => i.Partido)
                    .ThenInclude(p => p.Torneo)
                .Include(i => i.Equipo)
                .Where(i => i.Jugador.Dni == dni)
                .OrderBy(i => i.FechaRegistro)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
