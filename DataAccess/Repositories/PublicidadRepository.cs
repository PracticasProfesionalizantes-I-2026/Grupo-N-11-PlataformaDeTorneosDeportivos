using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataAccess.Data;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public class PublicidadRepository : GenericRepository<Publicidad>, IPublicidadRepository
    {
        public PublicidadRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Publicidad>> GetByTorneoIdAsync(Guid torneoId)
        {
            return await _dbSet
                .Where(p => (p.TorneoId == torneoId || p.TorneoId == null) && p.Activo)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task IncrementImpressionsAsync(IEnumerable<Guid> bannerIds)
        {
            var ids = bannerIds.ToList();
            if (ids.Count == 0) return;

            var banners = await _dbSet.Where(p => ids.Contains(p.Id)).ToListAsync();
            foreach (var b in banners)
            {
                b.Impresiones++;
            }
            await _context.SaveChangesAsync();
        }

        public async Task<int> IncrementClickAsync(Guid bannerId)
        {
            var banner = await _dbSet.FirstOrDefaultAsync(p => p.Id == bannerId);
            if (banner != null)
            {
                banner.Clicks++;
                await _context.SaveChangesAsync();
                return banner.Clicks;
            }
            return 0;
        }
    }
}
