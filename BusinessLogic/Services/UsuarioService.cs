using System;
using System.Linq;
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
            if (dto == null)
            {
                throw new ValidationException("Los datos de registro son obligatorios.");
            }

            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            var normalizedDni = dto.Dni?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedDni) || !normalizedDni.All(c => c >= '0' && c <= '9'))
            {
                throw new ValidationException("El DNI debe contener solo números.");
            }

            // RN-10: Verificar que ni el email ni el DNI se encuentren duplicados en la base de datos
            var usuarioPorEmail = await _usuarioRepository.GetByEmailAsync(normalizedEmail);
            if (usuarioPorEmail != null)
            {
                throw new UsuarioDuplicadoException();
            }

            var usuarioPorDni = await _usuarioRepository.GetByDniAsync(normalizedDni);
            if (usuarioPorDni != null)
            {
                throw new UsuarioDuplicadoException();
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
                Activo = usuario.Activo,
                FechaCreacion = usuario.FechaCreacion,
                Mensaje = "Registro exitoso. Revise su correo para activar la cuenta."
            };
        }
    }
}
