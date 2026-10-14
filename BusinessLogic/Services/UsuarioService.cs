using System;
using System.Threading.Tasks;
using BusinessLogic.Security;
using DataAccess.Entities;
using DataAccess.Repositories;
using Shared.DTOs;
using Shared.Exceptions;

namespace BusinessLogic.Services
{
    public class UsuarioService : IUsuarioService
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IPasswordHasher _passwordHasher;

        public UsuarioService(
            IUsuarioRepository usuarioRepository,
            IPasswordHasher passwordHasher)
        {
            _usuarioRepository = usuarioRepository;
            _passwordHasher = passwordHasher;
        }

        public async Task<UsuarioResponseDTO> RegisterUsuarioAsync(UsuarioRegistroDTO dto)
        {
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            var normalizedDni = dto.Dni.Trim();

            // RN-10: Verificar que ni el email ni el DNI se encuentren duplicados en la base de datos
            var usuarioPorEmail = await _usuarioRepository.GetByEmailAsync(normalizedEmail);
            if (usuarioPorEmail != null)
            {
                throw new BusinessRuleConflictException("El correo o DNI ya se encuentra registrado en la plataforma.");
            }

            var usuarioPorDni = await _usuarioRepository.GetByDniAsync(normalizedDni);
            if (usuarioPorDni != null)
            {
                throw new BusinessRuleConflictException("El correo o DNI ya se encuentra registrado en la plataforma.");
            }

            // Hashear contraseña
            var passwordHash = _passwordHasher.HashPassword(dto.Password);

            // Mapeo manual de DTO a entidad de dominio
            var usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                Nombre = dto.Nombre.Trim(),
                Apellido = dto.Apellido.Trim(),
                Dni = normalizedDni,
                Email = normalizedEmail,
                Telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim(),
                PasswordHash = passwordHash,
                Rol = dto.Rol,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            await _usuarioRepository.AddAsync(usuario);

            // Mapeo manual de entidad a DTO de salida
            return new UsuarioResponseDTO
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Apellido = usuario.Apellido,
                Dni = usuario.Dni,
                Email = usuario.Email,
                Telefono = usuario.Telefono,
                Rol = usuario.Rol.ToString(),
                Activo = usuario.Activo,
                FechaCreacion = usuario.FechaCreacion,
                Mensaje = "Registro exitoso. Revise su correo para activar la cuenta."
            };
        }
    }
}
