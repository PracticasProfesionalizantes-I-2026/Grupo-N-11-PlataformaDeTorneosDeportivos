using System;
using System.Threading.Tasks;
using Shared.Enums;

namespace DataAccess.Repositories
{
    public class DisciplinaRepository : IDisciplinaRepository
    {
        public Task<bool> ExistsAndIsActiveAsync(int disciplinaId)
        {
            // Valida existencia según las disciplinas registradas en el sistema (ej. Fútbol=0, E-Sports=1)
            bool exists = Enum.IsDefined(typeof(Disciplina), disciplinaId);
            return Task.FromResult(exists);
        }
    }
}
