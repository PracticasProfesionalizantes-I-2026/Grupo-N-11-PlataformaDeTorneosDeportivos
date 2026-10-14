using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusinessLogic.Security;
using DataAccess.Entities;
using DataAccess.Repositories;
using Shared.DTOs;
using Shared.Enums;
using Shared.Exceptions;

namespace BusinessLogic.Services
{
    public class VeedorService : IVeedorService
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IPasswordHasher _passwordHasher;

        public VeedorService(
            IUsuarioRepository usuarioRepository,
            IPasswordHasher passwordHasher)
        {
            _usuarioRepository = usuarioRepository;
            _passwordHasher = passwordHasher;
        }

        public async Task<VeedorResponseDTO> CreateVeedorAsync(VeedorCreateDTO dto)
        {
            if (dto == null)
            {
                throw new ValidationException("Los datos del veedor son obligatorios.");
            }

            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            var normalizedDni = dto.Dni.Trim();

            // 3a. Verificar conflicto de DNI o Email existente en la plataforma
            var usuarioPorEmail = await _usuarioRepository.GetByEmailAsync(normalizedEmail);
            if (usuarioPorEmail != null)
            {
                throw new BusinessRuleConflictException("El DNI o Email ya están registrados en el sistema");
            }

            var usuarioPorDni = await _usuarioRepository.GetByDniAsync(normalizedDni);
            if (usuarioPorDni != null)
            {
                throw new BusinessRuleConflictException("El DNI o Email ya están registrados en el sistema");
            }

            // Generar contraseña inicial automática y hashearla
            var tempPassword = $"Veedor_{Guid.NewGuid():N}".Substring(0, 10) + "!";
            var passwordHash = _passwordHasher.HashPassword(tempPassword);

            var usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                Nombre = dto.Nombre.Trim(),
                Apellido = dto.Apellido.Trim(),
                Dni = normalizedDni,
                Email = normalizedEmail,
                Telefono = dto.Telefono?.Trim(),
                PasswordHash = passwordHash,
                Rol = RolUsuario.Veedor,
                Activo = true,
                SedeAsignada = dto.SedeAsignada?.Trim(),
                TorneosAsignadosIds = dto.TorneosAsignados != null && dto.TorneosAsignados.Any()
                    ? string.Join(",", dto.TorneosAsignados)
                    : null,
                FechaCreacion = DateTime.UtcNow
            };

            await _usuarioRepository.AddAsync(usuario);

            return MapToResponseDTO(usuario);
        }

        public async Task<VeedorResponseDTO> UpdateVeedorAsync(Guid id, VeedorUpdateDTO dto)
        {
            if (dto == null)
            {
                throw new ValidationException("Los datos de actualización son obligatorios.");
            }

            var usuario = await _usuarioRepository.GetByIdAsync(id);
            if (usuario == null || usuario.Rol != RolUsuario.Veedor)
            {
                throw new NotFoundException($"No se encontró el veedor con ID {id}");
            }

            usuario.Nombre = dto.Nombre.Trim();
            usuario.Apellido = dto.Apellido.Trim();
            usuario.Telefono = dto.Telefono?.Trim();
            usuario.SedeAsignada = dto.SedeAsignada?.Trim();

            if (dto.TorneosAsignados != null)
            {
                usuario.TorneosAsignadosIds = string.Join(",", dto.TorneosAsignados);
            }

            await _usuarioRepository.UpdateAsync(usuario);

            return MapToResponseDTO(usuario);
        }

        public async Task DeactivateVeedorAsync(Guid id)
        {
            var usuario = await _usuarioRepository.GetByIdAsync(id);
            if (usuario == null || usuario.Rol != RolUsuario.Veedor)
            {
                throw new NotFoundException($"No se encontró el veedor con ID {id}");
            }

            usuario.Activo = false;
            await _usuarioRepository.UpdateAsync(usuario);
        }

        public async Task<VeedorResponseDTO> GetByIdAsync(Guid id)
        {
            var usuario = await _usuarioRepository.GetByIdAsync(id);
            if (usuario == null || usuario.Rol != RolUsuario.Veedor)
            {
                throw new NotFoundException($"No se encontró el veedor con ID {id}");
            }

            return MapToResponseDTO(usuario);
        }

        public async Task<IEnumerable<VeedorResponseDTO>> GetAllAsync()
        {
            var veedores = await _usuarioRepository.FindAsync(u => u.Rol == RolUsuario.Veedor);
            return veedores.Select(MapToResponseDTO);
        }

        private static VeedorResponseDTO MapToResponseDTO(Usuario usuario)
        {
            var torneos = new List<Guid>();
            if (!string.IsNullOrWhiteSpace(usuario.TorneosAsignadosIds))
            {
                var ids = usuario.TorneosAsignadosIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var idStr in ids)
                {
                    if (Guid.TryParse(idStr, out var torneoId))
                    {
                        torneos.Add(torneoId);
                    }
                }
            }

            return new VeedorResponseDTO
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Apellido = usuario.Apellido,
                Dni = usuario.Dni,
                Email = usuario.Email,
                Telefono = usuario.Telefono,
                Rol = usuario.Rol.ToString(),
                Activo = usuario.Activo,
                TorneosAsignados = torneos,
                SedeAsignada = usuario.SedeAsignada,
                FechaCreacion = usuario.FechaCreacion
            };
        }
    }
}
