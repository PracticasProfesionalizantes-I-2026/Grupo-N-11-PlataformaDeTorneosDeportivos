using System;
using System.Threading.Tasks;
using BusinessLogic.Security;
using DataAccess.Repositories;
using Shared.DTOs;
using Shared.Exceptions;

namespace BusinessLogic.Services
{
    public class RecuperacionPasswordService : IRecuperacionPasswordService
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IPasswordHasher _passwordHasher;

        public RecuperacionPasswordService(IUsuarioRepository usuarioRepository, IPasswordHasher passwordHasher)
        {
            _usuarioRepository = usuarioRepository;
            _passwordHasher = passwordHasher;
        }

        public async Task<bool> SolicitarRecuperacionAsync(RecuperarPasswordDTO dto)
        {
            var emailNormalizado = dto.Email.Trim().ToLowerInvariant();
            var usuario = await _usuarioRepository.GetByEmailAsync(emailNormalizado);

            // Flujo Alternativo 3a: Finalización silenciosa si el correo no existe
            if (usuario == null)
            {
                return true;
            }

            // Flujo Principal: Generar token único con expiración de 24 horas
            usuario.PasswordResetToken = Guid.NewGuid().ToString("N");
            usuario.PasswordResetTokenExpiration = DateTime.UtcNow.AddHours(24);

            await _usuarioRepository.UpdateAsync(usuario);

            return true;
        }

        public async Task<bool> ResetearPasswordAsync(ResetPasswordDTO dto)
        {
            // Paso 7 del CU-03: Buscar usuario por token de recuperación
            var usuario = await _usuarioRepository.GetByResetTokenAsync(dto.Token);

            // Flujo Alternativo 7a: Token inexistente
            if (usuario == null)
            {
                throw new ValidationException("El enlace de recuperación ha expirado, intenta solicitar uno nuevo.");
            }

            // Flujo Alternativo 7a: Token expirado
            if (!usuario.PasswordResetTokenExpiration.HasValue || usuario.PasswordResetTokenExpiration.Value < DateTime.UtcNow)
            {
                throw new ValidationException("El enlace de recuperación ha expirado, intenta solicitar uno nuevo.");
            }

            // Flujo Principal: Hashear nueva contraseña con PBKDF2 e invalidar el token
            usuario.PasswordHash = _passwordHasher.HashPassword(dto.NewPassword);
            usuario.PasswordResetToken = null;
            usuario.PasswordResetTokenExpiration = null;

            await _usuarioRepository.UpdateAsync(usuario);

            return true;
        }
    }
}
