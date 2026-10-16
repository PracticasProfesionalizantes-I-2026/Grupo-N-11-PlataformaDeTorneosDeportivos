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
    public class TorneoService : ITorneoService
    {
        private readonly ITorneoRepository _torneoRepository;
        private readonly ICuentaRecaudadoraService? _cuentaRecaudadoraService;
        private readonly IPartidoRepository? _partidoRepository;

        public TorneoService(
            ITorneoRepository torneoRepository,
            ICuentaRecaudadoraService? cuentaRecaudadoraService = null,
            IPartidoRepository? partidoRepository = null)
        {
            _torneoRepository = torneoRepository;
            _cuentaRecaudadoraService = cuentaRecaudadoraService;
            _partidoRepository = partidoRepository;
        }

        public async Task<IEnumerable<TorneoResponseDTO>> GetAllAsync()
        {
            var torneos = await _torneoRepository.GetAllAsync();
            return torneos.Select(MapToResponseDTO);
        }

        public async Task<TorneoResponseDTO> GetByIdAsync(Guid id)
        {
            var torneo = await _torneoRepository.GetByIdAsync(id);
            if (torneo == null)
                throw new NotFoundException($"No se encontró el torneo con ID {id}");

            return MapToResponseDTO(torneo);
        }

        public async Task<TorneoResponseDTO> CreateTorneoAsync(TorneoCreateDTO dto)
        {
            // Paso 3 del CU-04: Normalización y verificación de existencia de disciplina
            if (dto.DisciplinaId.HasValue)
            {
                if (!Enum.IsDefined(typeof(Disciplina), dto.DisciplinaId.Value))
                {
                    throw new DisciplinaNotFoundException("La disciplina especificada no existe");
                }
                dto.Disciplina = (Disciplina)dto.DisciplinaId.Value;
            }
            else if (!Enum.IsDefined(typeof(Disciplina), dto.Disciplina))
            {
                throw new DisciplinaNotFoundException("La disciplina especificada no existe");
            }

            var normalizedName = dto.Nombre?.Trim() ?? string.Empty;

            // Paso 3 del CU-04: Comprobar que no exista otro torneo con el mismo nombre
            var torneoExistente = await _torneoRepository.GetByNombreAsync(normalizedName);
            if (torneoExistente != null)
            {
                throw new TorneoNombreDuplicadoException("Ya existe un torneo con el nombre ingresado");
            }

            // Validar reglas de negocio
            await ValidateRulesAsync(dto);

            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = normalizedName,
                Disciplina = dto.Disciplina,
                Modalidad = dto.Modalidad,
                CupoMaximo = dto.CupoMaximo,
                EsPago = dto.EsPago || dto.CostoInscripcion > 0,
                CostoInscripcion = dto.CostoInscripcion,
                Estado = EstadoTorneo.Abierto
            };

            await _torneoRepository.AddAsync(torneo);
            return MapToResponseDTO(torneo);
        }

        public Task<TorneoResponseDTO> CreateAsync(TorneoCreateDTO dto)
        {
            return CreateTorneoAsync(dto);
        }

        public async Task<TorneoResponseDTO> UpdateAsync(Guid id, TorneoUpdateDTO dto)
        {
            var torneo = await _torneoRepository.GetByIdAsync(id);
            if (torneo == null)
                throw new NotFoundException($"No se encontró el torneo con ID {id}");

            var createEquivalent = new TorneoCreateDTO
            {
                Nombre = dto.Nombre,
                Disciplina = dto.Disciplina,
                DisciplinaId = dto.DisciplinaId,
                Modalidad = dto.Modalidad,
                CupoMaximo = dto.CupoMaximo,
                EsPago = dto.EsPago,
                CostoInscripcion = dto.CostoInscripcion,
                CuentaRecaudadoraActiva = dto.CuentaRecaudadoraActiva
            };

            await ValidateRulesAsync(createEquivalent);

            torneo.Nombre = dto.Nombre.Trim();
            torneo.Disciplina = dto.Disciplina;
            torneo.Modalidad = dto.Modalidad;
            torneo.CupoMaximo = dto.CupoMaximo;
            torneo.EsPago = dto.EsPago || dto.CostoInscripcion > 0;
            torneo.CostoInscripcion = dto.CostoInscripcion;

            await _torneoRepository.UpdateAsync(torneo);
            return MapToResponseDTO(torneo);
        }

        public async Task DeleteAsync(Guid id)
        {
            var torneo = await _torneoRepository.GetByIdAsync(id);
            if (torneo == null)
                throw new NotFoundException($"No se encontró el torneo con ID {id}");

            await _torneoRepository.DeleteAsync(torneo);
        }

        private async Task ValidateRulesAsync(TorneoCreateDTO dto)
        {
            // RN-04: Eliminación directa requiere cupo mínimo de 4 y potencia de 2
            if (dto.Modalidad == Modalidad.EliminacionDirecta)
            {
                if (dto.CupoMaximo < 4)
                {
                    throw new BusinessRuleConflictException("Los torneos de Eliminación Directa requieren un cupo mínimo de 4 equipos.");
                }

                bool esPotenciaDe2 = (dto.CupoMaximo & (dto.CupoMaximo - 1)) == 0;
                if (!esPotenciaDe2)
                {
                    throw new CupoInvalidoEliminacionException("Para torneos de eliminación directa el cupo debe ser potencia de 2 y mínimo 4 (RN-04)");
                }
            }

            // Validar monto no negativo
            if (dto.CostoInscripcion < 0)
            {
                throw new ValidationException("El costo de inscripción no puede ser negativo.");
            }

            // RN-05: Si el torneo es de pago, costo > 0 y cuenta recaudadora activa obligatoria
            if (dto.EsPago)
            {
                if (dto.CostoInscripcion <= 0)
                {
                    throw new CostoInvalidoException("El costo debe ser mayor a cero para torneos pagos");
                }

                bool cuentaActiva = true;
                if (_cuentaRecaudadoraService != null)
                {
                    cuentaActiva = await _cuentaRecaudadoraService.IsCuentaActivaAsync();
                }

                if (dto.CuentaRecaudadoraActiva.HasValue)
                {
                    cuentaActiva = dto.CuentaRecaudadoraActiva.Value;
                }

                if (!cuentaActiva)
                {
                    throw new CuentaRecaudadoraInactivaException("No se puede publicar un torneo pago sin una cuenta recaudadora activa (RN-05)");
                }
            }
        }

        public async Task<IEnumerable<PosicionTablaDTO>> GetPosicionesAsync(Guid torneoId)
        {
            var torneo = await _torneoRepository.GetTorneoConEquiposAsync(torneoId);
            if (torneo == null)
            {
                torneo = await _torneoRepository.GetByIdAsync(torneoId);
            }
            if (torneo == null)
            {
                throw new NotFoundException($"No se encontró el torneo con ID {torneoId}");
            }

            if (torneo.Modalidad != Modalidad.Liga)
            {
                throw new ValidationException("La vista solicitada no es compatible con el formato del torneo.");
            }

            IEnumerable<Partido> partidos = _partidoRepository != null
                ? await _partidoRepository.GetPartidosByTorneoIdAsync(torneoId)
                : (torneo.Partidos ?? Enumerable.Empty<Partido>());

            var listaPartidos = partidos.ToList();
            if (listaPartidos.Count == 0 && torneo.Partidos != null && torneo.Partidos.Count > 0)
            {
                listaPartidos = torneo.Partidos.ToList();
            }

            var equipos = (torneo.Equipos != null && torneo.Equipos.Any(e => e.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado))
                ? torneo.Equipos.Where(e => e.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado).ToList()
                : (torneo.Equipos?.ToList() ?? new List<Equipo>());

            var tabla = new Dictionary<Guid, PosicionTablaDTO>();

            foreach (var eq in equipos)
            {
                tabla[eq.Id] = new PosicionTablaDTO
                {
                    EquipoId = eq.Id,
                    EquipoNombre = eq.Nombre
                };
            }

            var partidosFinalizados = listaPartidos
                .Where(p => p.Estado == EstadoPartido.Finalizado || (p.MarcadorLocal.HasValue && p.MarcadorVisitante.HasValue))
                .ToList();

            foreach (var p in partidosFinalizados)
            {
                if (!p.EquipoLocalId.HasValue || !p.EquipoVisitanteId.HasValue)
                    continue;

                var localId = p.EquipoLocalId.Value;
                var visId = p.EquipoVisitanteId.Value;

                if (!tabla.TryGetValue(localId, out var localStats))
                {
                    localStats = new PosicionTablaDTO
                    {
                        EquipoId = localId,
                        EquipoNombre = p.EquipoLocal?.Nombre ?? "Equipo Local"
                    };
                    tabla[localId] = localStats;
                }

                if (!tabla.TryGetValue(visId, out var visStats))
                {
                    visStats = new PosicionTablaDTO
                    {
                        EquipoId = visId,
                        EquipoNombre = p.EquipoVisitante?.Nombre ?? "Equipo Visitante"
                    };
                    tabla[visId] = visStats;
                }

                int ml = p.MarcadorLocal ?? 0;
                int mv = p.MarcadorVisitante ?? 0;

                localStats.PartidosJugados++;
                visStats.PartidosJugados++;

                localStats.GolesAFavor += ml;
                localStats.GolesEnContra += mv;
                visStats.GolesAFavor += mv;
                visStats.GolesEnContra += ml;

                if (ml > mv)
                {
                    localStats.PartidosGanados++;
                    localStats.Puntos += 3;
                    visStats.PartidosPerdidos++;
                }
                else if (mv > ml)
                {
                    visStats.PartidosGanados++;
                    visStats.Puntos += 3;
                    localStats.PartidosPerdidos++;
                }
                else
                {
                    localStats.PartidosEmpatados++;
                    localStats.Puntos += 1;
                    visStats.PartidosEmpatados++;
                    visStats.Puntos += 1;
                }
            }

            foreach (var stat in tabla.Values)
            {
                stat.DiferenciaGoles = stat.GolesAFavor - stat.GolesEnContra;
            }

            var resultadoOrdenado = tabla.Values
                .OrderByDescending(t => t.Puntos)
                .ThenByDescending(t => t.DiferenciaGoles)
                .ThenByDescending(t => t.GolesAFavor)
                .ThenBy(t => t.EquipoNombre, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < resultadoOrdenado.Count; i++)
            {
                resultadoOrdenado[i].Posicion = i + 1;
            }

            return resultadoOrdenado;
        }

        public async Task<BracketTorneoDTO> GetLlavesAsync(Guid torneoId)
        {
            var torneo = await _torneoRepository.GetTorneoConEquiposAsync(torneoId);
            if (torneo == null)
            {
                torneo = await _torneoRepository.GetByIdAsync(torneoId);
            }
            if (torneo == null)
            {
                throw new NotFoundException($"No se encontró el torneo con ID {torneoId}");
            }

            if (torneo.Modalidad != Modalidad.EliminacionDirecta)
            {
                throw new ValidationException("La vista solicitada no es compatible con el formato del torneo.");
            }

            IEnumerable<Partido> partidos = _partidoRepository != null
                ? await _partidoRepository.GetPartidosByTorneoIdAsync(torneoId)
                : (torneo.Partidos ?? Enumerable.Empty<Partido>());

            var listaPartidos = partidos.ToList();
            if (listaPartidos.Count == 0 && torneo.Partidos != null && torneo.Partidos.Count > 0)
            {
                listaPartidos = torneo.Partidos.ToList();
            }

            var nombresEquipos = torneo.Equipos?.ToDictionary(e => e.Id, e => e.Nombre) ?? new Dictionary<Guid, string>();

            return MapToBracketTorneoDTO(torneo, listaPartidos, nombresEquipos);
        }

        private static BracketTorneoDTO MapToBracketTorneoDTO(Torneo torneo, List<Partido> partidos, Dictionary<Guid, string> nombresEquipos)
        {
            var rondasAgrupadas = partidos
                .GroupBy(p => p.Ronda)
                .OrderBy(g => g.Key)
                .ToList();

            int totalRondas = rondasAgrupadas.Any() ? rondasAgrupadas.Max(g => g.Key) : 1;

            var rondasDto = new List<RondaBracketDTO>();

            foreach (var grupo in rondasAgrupadas)
            {
                int numeroRonda = grupo.Key;
                string nombreRonda = ObtenerNombreRonda(numeroRonda, totalRondas);

                var partidosDto = grupo
                    .OrderBy(p => p.FechaHora)
                    .Select(p => MapToPartidoBracketDTO(p, nombresEquipos))
                    .ToList();

                rondasDto.Add(new RondaBracketDTO
                {
                    NumeroRonda = numeroRonda,
                    NombreRonda = nombreRonda,
                    Partidos = partidosDto
                });
            }

            return new BracketTorneoDTO
            {
                TorneoId = torneo.Id,
                TorneoNombre = torneo.Nombre,
                TotalRondas = totalRondas,
                Rondas = rondasDto
            };
        }

        private static string ObtenerNombreRonda(int numeroRonda, int totalRondas)
        {
            int rondasRestantes = totalRondas - numeroRonda;
            return rondasRestantes switch
            {
                0 => "Final",
                1 => "Semifinal",
                2 => "Cuartos de Final",
                3 => "Octavos de Final",
                _ => $"Ronda {numeroRonda}"
            };
        }

        private static PartidoBracketDTO MapToPartidoBracketDTO(Partido p, Dictionary<Guid, string>? nombresEquipos = null)
        {
            Guid? ganadorId = null;
            string? ganadorNombre = null;

            string localNombre = p.EquipoLocal?.Nombre 
                ?? (p.EquipoLocalId.HasValue && nombresEquipos != null && nombresEquipos.TryGetValue(p.EquipoLocalId.Value, out var nl) ? nl : "Por Definir");

            string visNombre = p.EquipoVisitante?.Nombre 
                ?? (p.EquipoVisitanteId.HasValue && nombresEquipos != null && nombresEquipos.TryGetValue(p.EquipoVisitanteId.Value, out var nv) ? nv : "Por Definir");

            if (p.MarcadorLocal.HasValue && p.MarcadorVisitante.HasValue)
            {
                if (p.MarcadorLocal.Value > p.MarcadorVisitante.Value)
                {
                    ganadorId = p.EquipoLocalId;
                    ganadorNombre = localNombre;
                }
                else if (p.MarcadorVisitante.Value > p.MarcadorLocal.Value)
                {
                    ganadorId = p.EquipoVisitanteId;
                    ganadorNombre = visNombre;
                }
                else if (p.PenalesLocal.HasValue && p.PenalesVisitante.HasValue)
                {
                    if (p.PenalesLocal.Value > p.PenalesVisitante.Value)
                    {
                        ganadorId = p.EquipoLocalId;
                        ganadorNombre = localNombre;
                    }
                    else if (p.PenalesVisitante.Value > p.PenalesLocal.Value)
                    {
                        ganadorId = p.EquipoVisitanteId;
                        ganadorNombre = visNombre;
                    }
                }
            }

            return new PartidoBracketDTO
            {
                Id = p.Id,
                Ronda = p.Ronda,
                EquipoLocalId = p.EquipoLocalId,
                EquipoLocalNombre = localNombre,
                EquipoVisitanteId = p.EquipoVisitanteId,
                EquipoVisitanteNombre = visNombre,
                MarcadorLocal = p.MarcadorLocal,
                MarcadorVisitante = p.MarcadorVisitante,
                PenalesLocal = p.PenalesLocal,
                PenalesVisitante = p.PenalesVisitante,
                Estado = p.Estado.ToString(),
                GanadorId = ganadorId,
                GanadorNombre = ganadorNombre
            };
        }

        private static TorneoResponseDTO MapToResponseDTO(Torneo torneo)
        {
            return new TorneoResponseDTO
            {
                Id = torneo.Id,
                Nombre = torneo.Nombre,
                Disciplina = torneo.Disciplina.ToString(),
                Modalidad = torneo.Modalidad.ToString(),
                CupoMaximo = torneo.CupoMaximo,
                EsPago = torneo.EsPago,
                CostoInscripcion = torneo.CostoInscripcion,
                Estado = torneo.Estado.ToString()
            };
        }
    }
}
