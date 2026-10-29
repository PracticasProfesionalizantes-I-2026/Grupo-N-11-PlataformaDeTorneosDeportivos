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
        private readonly IPartidoRepository? _partidoRepository;

        public EquipoService(
            IEquipoRepository equipoRepository,
            ITorneoRepository torneoRepository,
            IJugadorRepository jugadorRepository,
            IPartidoRepository? partidoRepository = null)
        {
            _equipoRepository = equipoRepository;
            _torneoRepository = torneoRepository;
            _jugadorRepository = jugadorRepository;
            _partidoRepository = partidoRepository;
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
            if (torneo.Estado != EstadoTorneo.Abierto)
            {
                throw new TorneoInscripcionCerradaException("El torneo no se encuentra disponible para nuevas inscripciones");
            }

            // 3g. RN-06: Verificar si el torneo ya alcanzó su cupo máximo de equipos confirmados
            if (torneo.CupoMaximo > 0)
            {
                var equiposConfirmados = await _equipoRepository.FindAsync(e =>
                    e.TorneoId == torneoId && e.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado);
                if (equiposConfirmados != null && equiposConfirmados.Count() >= torneo.CupoMaximo)
                {
                    throw new TorneoCupoLlenoException("No se puede registrar el equipo: el torneo ya ha alcanzado su cupo máximo de equipos.");
                }
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

            // 3d. Validar duplicidad interna de DNI en la nómina enviada y formato numérico
            foreach (var jugador in dto.Jugadores)
            {
                var dniTrim = jugador.Dni?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(dniTrim) || !dniTrim.All(c => c >= '0' && c <= '9'))
                {
                    throw new ValidationException("El DNI debe contener solo números.");
                }
            }

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

            if (nuevoEstado == EstadoInscripcion.InscritoYConfirmado)
            {
                var torneo = await _torneoRepository.GetByIdAsync(equipo.TorneoId);
                if (torneo != null && torneo.Estado == EstadoTorneo.Abierto)
                {
                    var confirmados = await _equipoRepository.FindAsync(e => e.TorneoId == equipo.TorneoId && e.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado);
                    if (confirmados != null && confirmados.Count() >= torneo.CupoMaximo)
                    {
                        torneo.Estado = EstadoTorneo.InscripcionesCerradas;
                        await _torneoRepository.UpdateAsync(torneo);
                    }
                }
            }

            return MapToResponseDTO(equipo);
        }

        private static int GetMinimoJugadoresPorDisciplina(Disciplina disciplina) => disciplina switch
        {
            Disciplina.Futbol => 5,
            Disciplina.ESports => 3,
            _ => 1
        };

        public async Task<EquipoEstadisticasDTO> GetEstadisticasAsync(Guid id)
        {
            var equipo = await _equipoRepository.GetByIdWithTorneoAndJugadoresAsync(id);
            if (equipo == null)
            {
                equipo = await _equipoRepository.GetByIdAsync(id);
            }
            if (equipo == null)
            {
                throw new NotFoundException($"No se encontró el equipo con ID {id}");
            }

            Torneo? torneo = equipo.Torneo;
            if (torneo == null && equipo.TorneoId != Guid.Empty)
            {
                torneo = await _torneoRepository.GetByIdAsync(equipo.TorneoId);
            }

            var jugadores = equipo.Jugadores?.ToList() ?? new List<Jugador>();
            if (jugadores.Count == 0)
            {
                var recuperados = await _jugadorRepository.FindAsync(j => j.EquipoId == id);
                jugadores = recuperados.ToList();
            }

            IEnumerable<Partido> partidos = _partidoRepository != null
                ? await _partidoRepository.GetPartidosByEquipoIdAsync(id)
                : Enumerable.Empty<Partido>();

            var listaPartidos = partidos.ToList();
            if (listaPartidos.Count == 0 && _partidoRepository != null && equipo.TorneoId != Guid.Empty)
            {
                var partidosTorneo = await _partidoRepository.GetPartidosByTorneoIdAsync(equipo.TorneoId);
                listaPartidos = partidosTorneo
                    .Where(p => p.EquipoLocalId == id || p.EquipoVisitanteId == id)
                    .ToList();
            }

            int pj = 0;
            int pg = 0;
            int pe = 0;
            int pp = 0;
            int gf = 0;
            int gc = 0;

            var partidosFinalizados = listaPartidos
                .Where(p => p.Estado == EstadoPartido.Finalizado || (p.MarcadorLocal.HasValue && p.MarcadorVisitante.HasValue))
                .ToList();

            foreach (var p in partidosFinalizados)
            {
                pj++;
                bool esLocal = p.EquipoLocalId == id;
                int golesFavor = esLocal ? (p.MarcadorLocal ?? 0) : (p.MarcadorVisitante ?? 0);
                int golesContra = esLocal ? (p.MarcadorVisitante ?? 0) : (p.MarcadorLocal ?? 0);

                gf += golesFavor;
                gc += golesContra;

                if (golesFavor > golesContra)
                {
                    pg++;
                }
                else if (golesFavor < golesContra)
                {
                    pp++;
                }
                else
                {
                    if (p.PenalesLocal.HasValue && p.PenalesVisitante.HasValue)
                    {
                        int penFavor = esLocal ? p.PenalesLocal.Value : p.PenalesVisitante.Value;
                        int penContra = esLocal ? p.PenalesVisitante.Value : p.PenalesLocal.Value;
                        if (penFavor > penContra)
                            pg++;
                        else if (penFavor < penContra)
                            pp++;
                        else
                            pe++;
                    }
                    else
                    {
                        pe++;
                    }
                }
            }

            int dif = gf - gc;
            double efectividad = pj > 0 ? Math.Round(((double)(pg * 3 + pe) / (pj * 3)) * 100, 2) : 0.0;
            double porcentajeVictorias = pj > 0 ? Math.Round(((double)pg / pj) * 100, 2) : 0.0;

            var torneosHistorial = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (torneo != null && !string.IsNullOrWhiteSpace(torneo.Nombre))
            {
                torneosHistorial.Add(torneo.Nombre);
            }
            foreach (var p in listaPartidos)
            {
                if (p.Torneo != null && !string.IsNullOrWhiteSpace(p.Torneo.Nombre))
                {
                    torneosHistorial.Add(p.Torneo.Nombre);
                }
            }

            return MapToEstadisticasDTO(
                equipo, 
                torneo, 
                pj, 
                pg, 
                pe, 
                pp, 
                gf, 
                gc, 
                dif, 
                efectividad, 
                porcentajeVictorias, 
                torneosHistorial, 
                jugadores);
        }

        private static EquipoEstadisticasDTO MapToEstadisticasDTO(
            Equipo equipo,
            Torneo? torneo,
            int pj,
            int pg,
            int pe,
            int pp,
            int gf,
            int gc,
            int dif,
            double efectividad,
            double porcentajeVictorias,
            IEnumerable<string> torneosHistorial,
            IEnumerable<Jugador> jugadores)
        {
            var listaTorneos = torneosHistorial.ToList();
            var listaIntegrantes = jugadores.Select(j => new JugadorResponseDTO
            {
                Id = j.Id,
                Nombre = j.Nombre,
                Apellido = j.Apellido,
                Dni = j.Dni,
                DocumentacionUrl = j.DocumentacionUrl,
                EquipoId = j.EquipoId
            }).ToList();

            return new EquipoEstadisticasDTO
            {
                EquipoId = equipo.Id,
                Nombre = equipo.Nombre,
                Logo = equipo.Logo,
                TorneoActualId = equipo.TorneoId,
                TorneoActual = torneo?.Nombre ?? "Sin Torneo Asignado",
                PartidosJugados = pj,
                Victorias = pg,
                Empates = pe,
                Derrotas = pp,
                GolesAFavor = gf,
                GolesEnContra = gc,
                DiferenciaGoles = dif,
                Efectividad = efectividad,
                PorcentajeVictorias = porcentajeVictorias,
                TorneosDisputados = listaTorneos.Count,
                HistorialTorneos = listaTorneos,
                Integrantes = listaIntegrantes
            };
        }

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
