using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataAccess.Entities;
using DataAccess.Repositories;
using Shared.DTOs;
using Shared.Enums;
using Shared.Exceptions;

namespace BusinessLogic.Services
{
    public class EquipoService : IEquipoService
    {
        private readonly IEquipoRepository _equipoRepository;
        private readonly ITorneoRepository _torneoRepository;
        private readonly IJugadorRepository _jugadorRepository;

        public EquipoService(
            IEquipoRepository equipoRepository,
            ITorneoRepository torneoRepository,
            IJugadorRepository jugadorRepository)
        {
            _equipoRepository = equipoRepository;
            _torneoRepository = torneoRepository;
            _jugadorRepository = jugadorRepository;
        }

        public async Task<IEnumerable<EquipoResponseDTO>> GetByTorneoIdAsync(Guid torneoId)
        {
            var equipos = await _equipoRepository.GetByTorneoIdWithJugadoresAsync(torneoId);
            return equipos.Select(MapToResponseDTO);
        }

        public async Task<EquipoResponseDTO> GetByIdAsync(Guid id)
        {
            var equipo = await _equipoRepository.GetByIdAsync(id);
            if (equipo == null)
                throw new NotFoundException($"No se encontró el equipo con ID {id}");

            var jugadores = await _jugadorRepository.FindAsync(j => j.EquipoId == id);
            equipo.Jugadores = jugadores.ToList();

            return MapToResponseDTO(equipo);
        }

        public async Task<EquipoResponseDTO> CreateAsync(EquipoCreateDTO dto)
        {
            return await CreateEquipoAsync(dto.TorneoId, dto);
        }

        public async Task<EquipoResponseDTO> CreateEquipoAsync(EquipoCreateDTO dto)
        {
            return await CreateEquipoAsync(dto.TorneoId, dto);
        }

        public async Task<EquipoResponseDTO> CreateEquipoAsync(Guid torneoId, EquipoCreateDTO dto)
        {
            if (dto == null)
                throw new ValidationException("Los datos del equipo son requeridos.");

            var nombreNormalizado = dto.Nombre?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nombreNormalizado))
                throw new ValidationException("El nombre del equipo es obligatorio.");

            // 3a. Verificar existencia del torneo
            var torneo = await _torneoRepository.GetByIdAsync(torneoId);
            if (torneo == null)
                throw new TorneoNotFoundException("El torneo especificado no existe");

            // 3b. Verificar estado de inscripciones abiertas
            if (!string.Equals(torneo.Estado, "Abierto para Inscripción", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(torneo.Estado, "Abierto", StringComparison.OrdinalIgnoreCase))
            {
                throw new TorneoInscripcionCerradaException("El torneo no se encuentra disponible para nuevas inscripciones");
            }

            // 3f. Verificar duplicidad de nombre en el mismo torneo
            var existeNombre = await _equipoRepository.ExistsNombreInTorneoAsync(torneoId, nombreNormalizado);
            if (existeNombre)
            {
                throw new EquipoNombreDuplicadoException("Ya existe un equipo con ese nombre en este torneo");
            }

            // 3c. Validar mínimo reglamentario de jugadores según la disciplina
            var minimoRequerido = GetMinimoJugadoresPorDisciplina(torneo.Disciplina);
            if (dto.Jugadores == null || dto.Jugadores.Count < minimoRequerido)
            {
                throw new MinimoJugadoresInsuficienteException("La lista debe contener al menos el número mínimo de jugadores requeridos para la disciplina");
            }

            // 3d. Validar duplicidad interna de DNI en la nómina enviada
            var dnisEnviados = dto.Jugadores.Select(j => j.Dni?.Trim() ?? string.Empty).ToList();
            if (dnisEnviados.Distinct(StringComparer.OrdinalIgnoreCase).Count() < dnisEnviados.Count)
            {
                throw new ValidationException("Se encontraron DNIs duplicados en la lista de jugadores enviada");
            }

            // 3e. RN-02: Validar que ningún jugador esté ya registrado en otro equipo del mismo torneo
            var jugadoresEnTorneo = await _jugadorRepository.GetJugadoresByTorneoIdAsync(torneoId);
            var dnisExistentes = jugadoresEnTorneo
                .Select(j => j.Dni.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var jugadorDto in dto.Jugadores)
            {
                var dni = jugadorDto.Dni.Trim();
                if (dnisExistentes.Contains(dni))
                {
                    throw new JugadorYaInscriptoException(dni);
                }
            }

            // 4. RN-03: Crear equipo con estado Pendiente de Validación
            var equipo = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = nombreNormalizado,
                Logo = dto.LogoUrl ?? dto.Logo,
                EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                TorneoId = torneoId,
                Jugadores = dto.Jugadores.Select(j => new Jugador
                {
                    Id = Guid.NewGuid(),
                    Nombre = j.Nombre.Trim(),
                    Apellido = j.Apellido.Trim(),
                    Dni = j.Dni.Trim(),
                    DocumentacionUrl = j.DocumentacionUrl
                }).ToList()
            };

            await _equipoRepository.AddAsync(equipo);

            return MapToResponseDTO(equipo);
        }

        public async Task<EquipoResponseDTO> CambiarEstadoAsync(Guid id, string nuevoEstadoStr)
        {
            var equipo = await _equipoRepository.GetByIdAsync(id);
            if (equipo == null)
                throw new NotFoundException($"No se encontró el equipo con ID {id}");

            if (!Enum.TryParse<EstadoInscripcion>(nuevoEstadoStr, true, out var nuevoEstado))
                throw new ValidationException($"El estado '{nuevoEstadoStr}' no es válido.");

            if (nuevoEstado == EstadoInscripcion.InscritoYConfirmado)
            {
                var torneo = await _torneoRepository.GetByIdAsync(equipo.TorneoId);
                var equiposConfirmados = await _equipoRepository.FindAsync(e => e.TorneoId == equipo.TorneoId && e.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado);

                if (equiposConfirmados.Count() >= torneo!.CupoMaximo && equipo.EstadoInscripcion != EstadoInscripcion.InscritoYConfirmado)
                {
                    throw new BusinessRuleConflictException("El torneo ya ha alcanzado su cupo máximo de equipos confirmados.");
                }

                var jugadoresDelEquipo = await _jugadorRepository.FindAsync(j => j.EquipoId == id);
                if (!jugadoresDelEquipo.Any())
                {
                    throw new BusinessRuleConflictException("El equipo no cumple con el mínimo de jugadores requeridos (Mínimo 1) para ser confirmado.");
                }
            }

            equipo.EstadoInscripcion = nuevoEstado;
            await _equipoRepository.UpdateAsync(equipo);

            return MapToResponseDTO(equipo);
        }

        private static int GetMinimoJugadoresPorDisciplina(Disciplina disciplina) => disciplina switch
        {
            Disciplina.Futbol => 5,
            Disciplina.ESports => 3,
            _ => 1
        };

        private static EquipoResponseDTO MapToResponseDTO(Equipo equipo)
        {
            return new EquipoResponseDTO
            {
                Id = equipo.Id,
                Nombre = equipo.Nombre,
                Logo = equipo.Logo,
                EstadoInscripcion = equipo.EstadoInscripcion.ToString(),
                TorneoId = equipo.TorneoId,
                CantidadJugadores = equipo.Jugadores?.Count ?? 0,
                Jugadores = equipo.Jugadores?.Select(j => new JugadorResponseDTO
                {
                    Id = j.Id,
                    Nombre = j.Nombre,
                    Apellido = j.Apellido,
                    Dni = j.Dni,
                    DocumentacionUrl = j.DocumentacionUrl,
                    EquipoId = j.EquipoId
                }).ToList() ?? new List<JugadorResponseDTO>()
            };
        }
    }
}
