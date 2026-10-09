using System;
using System.Threading.Tasks;
using BusinessLogic.Security;
using DataAccess.Entities;
using DataAccess.Repositories;
using Shared.DTOs;
using Shared.Enums;
using Shared.Exceptions;

namespace BusinessLogic.Services
{
    public class UsuarioService : IUsuarioService
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IPasswordHasher _passwordHasher;

        public UsuarioService(IUsuarioRepository usuarioRepository, IPasswordHasher passwordHasher)
        {
            _usuarioRepository = usuarioRepository;
            _passwordHasher = passwordHasher;
        }

        public async Task<UsuarioResponseDTO> RegistrarAsync(UsuarioRegistroDTO dto)
        {
            // 1. Normalización de datos
            var dniNormalizado = dto.Dni.Trim();
            var emailNormalizado = dto.Email.Trim().ToLowerInvariant();

            // 2. Validación de Regla de Negocio RN-10: DNI y Email únicos
            var existeDni = await _usuarioRepository.ExistePorDniAsync(dniNormalizado);
            if (existeDni)
            {
                throw new BusinessRuleConflictException($"El DNI '{dniNormalizado}' ya se encuentra registrado en la plataforma.");
            }

            var existeEmail = await _usuarioRepository.ExistePorEmailAsync(emailNormalizado);
            if (existeEmail)
            {
                throw new BusinessRuleConflictException($"El correo electrónico '{emailNormalizado}' ya se encuentra registrado en la plataforma.");
            }

            // 3. Seguridad: Generación de hash criptográfico
            var passwordHash = _passwordHasher.HashPassword(dto.Password);

            // 4. Mapeo a entidad de dominio
            var nuevoUsuario = new Usuario
            {
                Nombre = dto.Nombre.Trim(),
                Apellido = dto.Apellido.Trim(),
                Dni = dniNormalizado,
                Email = emailNormalizado,
                Telefono = dto.Telefono?.Trim(),
                PasswordHash = passwordHash,
                Rol = dto.Rol ?? RolUsuario.Espectador,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            // 5. Persistencia física
            await _usuarioRepository.AddAsync(nuevoUsuario);

            // 6. Retorno como DTO
            return MapToResponseDTO(nuevoUsuario);
        }

        public async Task<UsuarioResponseDTO> GetByIdAsync(Guid id)
        {
            var usuario = await _usuarioRepository.GetByIdAsync(id);
            if (usuario == null)
            {
                throw new NotFoundException($"No se encontró el usuario con ID {id}");
            }

            return MapToResponseDTO(usuario);
        }

        private static UsuarioResponseDTO MapToResponseDTO(Usuario usuario)
        {
            return new UsuarioResponseDTO
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Apellido = usuario.Apellido,
                Dni = usuario.Dni,
                Email = usuario.Email,
                Telefono = usuario.Telefono,
                Rol = usuario.Rol.ToString(),
                FechaCreacion = usuario.FechaCreacion
            };
        }
    }
}
