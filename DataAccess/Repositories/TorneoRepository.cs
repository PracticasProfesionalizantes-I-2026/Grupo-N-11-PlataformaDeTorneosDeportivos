using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataAccess.Data;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public class TorneoRepository : GenericRepository<Torneo>, ITorneoRepository
    {
        public TorneoRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Torneo>> GetTorneosConEquiposAsync()
        {
            return await _dbSet.Include(t => t.Equipos).AsNoTracking().ToListAsync();
        }
    }
}
