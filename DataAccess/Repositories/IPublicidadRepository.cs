using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public interface IPublicidadRepository : IGenericRepository<Publicidad>
    {
        Task<IEnumerable<Publicidad>> GetByTorneoIdAsync(Guid torneoId);
        Task IncrementImpressionsAsync(IEnumerable<Guid> bannerIds);
        Task<int> IncrementClickAsync(Guid bannerId);
    }
}
