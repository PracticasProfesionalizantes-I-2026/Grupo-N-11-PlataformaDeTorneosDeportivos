using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataAccess.Entities;
using DataAccess.Repositories;
using Shared.DTOs;
using Shared.Exceptions;

namespace BusinessLogic.Services
{
    public class JugadorService : IJugadorService
    {
        private readonly IJugadorRepository _jugadorRepository;
        private readonly IEquipoRepository _equipoRepository;

        public JugadorService(IJugadorRepository jugadorRepository, IEquipoRepository equipoRepository)
        {
            _jugadorRepository = jugadorRepository;
            _equipoRepository = equipoRepository;
        }

        public async Task<IEnumerable<JugadorResponseDTO>> GetByEquipoIdAsync(Guid equipoId)
        {
            var jugadores = await _jugadorRepository.FindAsync(j => j.EquipoId == equipoId);
            return jugadores.Select(MapToResponseDTO);
        }

        public async Task<JugadorResponseDTO> CreateAsync(JugadorCreateDTO dto)
        {
            var equipoDestino = await _equipoRepository.GetByIdAsync(dto.EquipoId);
            if (equipoDestino == null)
                throw new NotFoundException($"No se encontró el equipo con ID {dto.EquipoId}");

            // Regla Estricta: Un jugador no puede estar inscrito en más de un equipo en un mismo torneo.
            var jugadoresEnTorneo = await _jugadorRepository.FindAsync(j => j.Dni == dto.Dni);
            
            foreach(var jugadorExistente in jugadoresEnTorneo)
            {
                var equipoExistente = await _equipoRepository.GetByIdAsync(jugadorExistente.EquipoId);
                if (equipoExistente != null && equipoExistente.TorneoId == equipoDestino.TorneoId)
                {
                    throw new BusinessRuleConflictException($"El jugador con DNI {dto.Dni} ya se encuentra inscrito en el equipo '{equipoExistente.Nombre}' del mismo torneo.");
                }
            }

            var jugador = new Jugador
            {
                Nombre = dto.Nombre,
                Apellido = dto.Apellido,
                Dni = dto.Dni,
                EquipoId = dto.EquipoId
            };

            await _jugadorRepository.AddAsync(jugador);
            return MapToResponseDTO(jugador);
        }

        public async Task DeleteAsync(Guid id)
        {
            var jugador = await _jugadorRepository.GetByIdAsync(id);
            if (jugador == null)
                throw new NotFoundException($"No se encontró el jugador con ID {id}");

            await _jugadorRepository.DeleteAsync(jugador);
        }

        private JugadorResponseDTO MapToResponseDTO(Jugador jugador)
        {
            return new JugadorResponseDTO
            {
                Id = jugador.Id,
                Nombre = jugador.Nombre,
                Apellido = jugador.Apellido,
                Dni = jugador.Dni,
                EquipoId = jugador.EquipoId
            };
        }
    }
}
