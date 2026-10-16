using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataAccess.Data;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public class EquipoRepository : GenericRepository<Equipo>, IEquipoRepository
    {
        public EquipoRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<bool> ExistsNombreInTorneoAsync(Guid torneoId, string nombre)
        {
            var nombreNormalizado = nombre.Trim().ToLower();
            return await _dbSet.AnyAsync(e => e.TorneoId == torneoId && e.Nombre.ToLower() == nombreNormalizado);
        }

        public async Task<IEnumerable<Equipo>> GetByTorneoIdWithJugadoresAsync(Guid torneoId)
        {
            return await _dbSet
                .Include(e => e.Jugadores)
                .Where(e => e.TorneoId == torneoId)
                .ToListAsync();
        }

        public async Task<Equipo?> GetByIdWithTorneoAndJugadoresAsync(Guid id)
        {
            return await _dbSet
                .Include(e => e.Torneo)
                .Include(e => e.Jugadores)
                .FirstOrDefaultAsync(e => e.Id == id);
        }
    }
}
