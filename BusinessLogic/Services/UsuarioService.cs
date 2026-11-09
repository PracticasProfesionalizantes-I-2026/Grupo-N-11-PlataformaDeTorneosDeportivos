using System;
using System.Threading.Tasks;
using BusinessLogic.Common;
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

            if (!Enum.IsDefined(typeof(Shared.Enums.RolUsuario), dto.Rol))
            {
                throw new ValidationException("El rol de usuario especificado no es válido.");
            }

            var normalizedDni = UsuarioValidationHelper.ValidateAndNormalizeDni(dto.Dni);
            var nombreTrim = UsuarioValidationHelper.ValidateAndNormalizeNombre(dto.Nombre);
            var apellidoTrim = UsuarioValidationHelper.ValidateAndNormalizeApellido(dto.Apellido);
            var normalizedEmail = UsuarioValidationHelper.ValidateAndNormalizeEmail(dto.Email);
            UsuarioValidationHelper.ValidatePassword(dto.Password);
            var telefonoTrim = UsuarioValidationHelper.ValidateAndNormalizeTelefono(dto.Telefono);

            await UsuarioValidationHelper.CheckUniquenessAsync(_usuarioRepository, normalizedEmail, normalizedDni);

            var passwordHash = _passwordHasher.HashPassword(dto.Password);

            // Prevención de Escalada de Privilegios (CU-01 / RNF-04):
            // En el registro público, un usuario nunca puede autoproclamarse con un rol administrativo o elevado
            // (Administrador, Organizador, Veedor). Se fuerza estrictamente al rol base (Capitan).
            var rolAsignado = (dto.Rol == Shared.Enums.RolUsuario.Administrador ||
                               dto.Rol == Shared.Enums.RolUsuario.Organizador ||
                               dto.Rol == Shared.Enums.RolUsuario.Veedor)
                ? Shared.Enums.RolUsuario.Capitan
                : dto.Rol;

            var usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                Nombre = nombreTrim,
                Apellido = apellidoTrim,
                Dni = normalizedDni,
                Email = normalizedEmail,
                Telefono = telefonoTrim,
                PasswordHash = passwordHash,
                Rol = rolAsignado,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            await _usuarioRepository.AddAsync(usuario);

            return MapToResponseDTO(usuario);
        }

        public async Task<UsuarioResponseDTO> CreateUsuarioAdminAsync(UsuarioRegistroDTO dto, string? rolSolicitante = null)
        {
            if (rolSolicitante != "Administrador" && rolSolicitante != "Organizador")
            {
                throw new ForbiddenException("No tiene permisos para dar de alta usuarios con roles administrativos.");
            }

            if (dto == null)
            {
                throw new ValidationException("Los datos de registro son obligatorios.");
            }

            if (!Enum.IsDefined(typeof(Shared.Enums.RolUsuario), dto.Rol))
            {
                throw new ValidationException("El rol de usuario especificado no es válido.");
            }

            var normalizedDni = UsuarioValidationHelper.ValidateAndNormalizeDni(dto.Dni);
            var nombreTrim = UsuarioValidationHelper.ValidateAndNormalizeNombre(dto.Nombre);
            var apellidoTrim = UsuarioValidationHelper.ValidateAndNormalizeApellido(dto.Apellido);
            var normalizedEmail = UsuarioValidationHelper.ValidateAndNormalizeEmail(dto.Email);
            UsuarioValidationHelper.ValidatePassword(dto.Password);
            var telefonoTrim = UsuarioValidationHelper.ValidateAndNormalizeTelefono(dto.Telefono);

            await UsuarioValidationHelper.CheckUniquenessAsync(_usuarioRepository, normalizedEmail, normalizedDni);

            var passwordHash = _passwordHasher.HashPassword(dto.Password);

            // En creación administrativa, el rol especificado por el administrador SÍ se respeta:
            var usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                Nombre = nombreTrim,
                Apellido = apellidoTrim,
                Dni = normalizedDni,
                Email = normalizedEmail,
                Telefono = telefonoTrim,
                PasswordHash = passwordHash,
                Rol = dto.Rol,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            await _usuarioRepository.AddAsync(usuario);

            return MapToResponseDTO(usuario);
        }

        public async Task<UsuarioResponseDTO> CambiarRolAsync(Guid id, CambiarRolDTO dto, string? rolSolicitante = null)
        {
            if (rolSolicitante != "Administrador" && rolSolicitante != "Organizador")
            {
                throw new ForbiddenException("No tiene permisos para asignar o modificar roles de usuarios.");
            }

            if (id == Guid.Empty)
            {
                throw new ValidationException("El identificador del usuario no puede ser vacío.");
            }

            if (dto == null || !Enum.IsDefined(typeof(Shared.Enums.RolUsuario), dto.NuevoRol))
            {
                throw new ValidationException("El nuevo rol especificado no es válido.");
            }

            var usuario = await _usuarioRepository.GetByIdAsync(id);
            if (usuario == null)
            {
                throw new NotFoundException($"No se encontró el usuario con ID {id}");
            }

            usuario.Rol = dto.NuevoRol;
            await _usuarioRepository.UpdateAsync(usuario);

            return MapToResponseDTO(usuario);
        }

        public async Task<UsuarioResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ValidationException("El identificador del usuario no puede ser vacío.");
            }

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
                Activo = usuario.Activo,
                FechaCreacion = usuario.FechaCreacion,
                Mensaje = "Registro exitoso. Revise su correo para activar la cuenta."
            };
        }
    }
}
