using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataAccess.Data;
using DataAccess.Entities;

namespace DataAccess.Repositories
{
    public class UsuarioRepository : GenericRepository<Usuario>, IUsuarioRepository
    {
        public UsuarioRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<Usuario?> GetByEmailOrDniAsync(string emailOrDni)
        {
            var clean = emailOrDni.Trim();
            var cleanLower = clean.ToLowerInvariant();
            return await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == cleanLower || u.Dni == clean);
        }

        public async Task<Usuario?> GetByEmailAsync(string email)
        {
            return await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<Usuario?> GetByDniAsync(string dni)
        {
            return await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Dni == dni);
        }

        public async Task<Usuario?> GetByPasswordResetTokenAsync(string token)
        {
            return await _dbSet
                .FirstOrDefaultAsync(u => u.PasswordResetToken == token);
        }
    }
}
