using DataAccess.Data;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public class EquipoRepository : GenericRepository<Equipo>, IEquipoRepository
    {
        public EquipoRepository(AppDbContext context) : base(context)
        {
        }
    }
}
