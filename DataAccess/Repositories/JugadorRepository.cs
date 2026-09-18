using DataAccess.Data;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public class JugadorRepository : GenericRepository<Jugador>, IJugadorRepository
    {
        public JugadorRepository(AppDbContext context) : base(context)
        {
        }
    }
}
