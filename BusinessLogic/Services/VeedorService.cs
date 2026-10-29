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
        private readonly IPartidoRepository _partidoRepository;

        public VeedorService(
            IUsuarioRepository usuarioRepository,
            IPasswordHasher passwordHasher,
            IPartidoRepository partidoRepository)
        {
            _usuarioRepository = usuarioRepository;
            _passwordHasher = passwordHasher;
            _partidoRepository = partidoRepository;
        }

        public async Task<VeedorResponseDTO> CreateVeedorAsync(VeedorCreateDTO dto)
        {
            if (dto == null)
            {
                throw new ValidationException("Los datos del veedor son obligatorios.");
            }

            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            var normalizedDni = dto.Dni?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedDni) || !normalizedDni.All(c => c >= '0' && c <= '9'))
            {
                throw new ValidationException("El DNI debe contener solo números.");
            }

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

        public async Task<IEnumerable<PartidoCronogramaResponseDTO>> GetPartidosByVeedorAsync(
            Guid veedorId, 
            DateTime? fecha = null, 
            Guid? torneoId = null, 
            Guid? currentUserId = null, 
            string? currentUserRol = null)
        {
            // RN Paso 3a: Acceso no autorizado (HTTP 403 Forbidden)
            if (currentUserId.HasValue && 
                currentUserId.Value != veedorId && 
                !string.Equals(currentUserRol, RolUsuario.Organizador.ToString(), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(currentUserRol, RolUsuario.Administrador.ToString(), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(currentUserRol, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException("No tiene permisos para consultar el cronograma de otro veedor.");
            }

            // Validar existencia del Veedor
            var veedor = await _usuarioRepository.GetByIdAsync(veedorId);
            if (veedor == null || veedor.Rol != RolUsuario.Veedor)
            {
                throw new NotFoundException($"No se encontró el veedor con ID {veedorId}");
            }

            // Extraer los torneos asignados al veedor
            var torneosIds = new List<Guid>();
            if (!string.IsNullOrWhiteSpace(veedor.TorneosAsignadosIds))
            {
                var ids = veedor.TorneosAsignadosIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var idStr in ids)
                {
                    if (Guid.TryParse(idStr, out var tId))
                    {
                        torneosIds.Add(tId);
                    }
                }
            }

            var partidos = await _partidoRepository.GetPartidosByVeedorAsync(veedorId, torneosIds, fecha, torneoId);

            return partidos.Select(MapToPartidoCronogramaDTO);
        }

        private static PartidoCronogramaResponseDTO MapToPartidoCronogramaDTO(Partido partido)
        {
            return new PartidoCronogramaResponseDTO
            {
                Id = partido.Id,
                TorneoId = partido.TorneoId,
                TorneoNombre = partido.Torneo?.Nombre ?? string.Empty,
                EquipoLocalId = partido.EquipoLocalId,
                EquipoLocalNombre = partido.EquipoLocal?.Nombre ?? "Equipo Local",
                EquipoVisitanteId = partido.EquipoVisitanteId,
                EquipoVisitanteNombre = partido.EquipoVisitante?.Nombre ?? "Equipo Visitante",
                FechaHora = partido.FechaHora,
                Sede = partido.Sede,
                Ronda = partido.Ronda,
                Estado = partido.Estado.ToString(),
                VeedorId = partido.VeedorId
            };
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
