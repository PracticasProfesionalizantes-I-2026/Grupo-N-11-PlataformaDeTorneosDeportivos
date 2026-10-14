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
    public class FixtureService : IFixtureService
    {
        private readonly ITorneoRepository _torneoRepository;
        private readonly IPartidoRepository _partidoRepository;

        public FixtureService(ITorneoRepository torneoRepository, IPartidoRepository partidoRepository)
        {
            _torneoRepository = torneoRepository;
            _partidoRepository = partidoRepository;
        }

        public async Task<GenerarFixtureResponseDTO> GenerarFixtureAsync(Guid torneoId, GenerarFixtureRequestDTO dto)
        {
            var torneo = await _torneoRepository.GetTorneoConEquiposAsync(torneoId);
            if (torneo == null)
            {
                // Fallback to GetByIdAsync if not found with includes
                torneo = await _torneoRepository.GetByIdAsync(torneoId);
            }

            if (torneo == null)
            {
                throw new NotFoundException($"No se encontró el torneo con ID {torneoId}");
            }

            // Validar estado del torneo: debe estar Abierto y sin fixture previo (Flujo 3b)
            if (torneo.Estado != EstadoTorneo.Abierto)
            {
                throw new TorneoEstadoInvalidoException("El torneo debe encontrarse en estado Abierto para generar el fixture.");
            }

            if (torneo.Partidos != null && torneo.Partidos.Count > 0)
            {
                throw new TorneoEstadoInvalidoException("El torneo ya tiene un fixture generado.");
            }

            // Validar cupo mínimo de equipos confirmados (Flujo 3a)
            var equiposConfirmados = torneo.Equipos
                .Where(e => e.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado)
                .ToList();

            if (equiposConfirmados.Count < 4)
            {
                throw new TorneoCupoMinimoNoAlcanzadoException("No se puede generar el fixture. No se alcanza el mínimo de equipos requeridos.");
            }

            // Obtener listas de horarios y sedes
            var horarios = (dto.Horarios != null && dto.Horarios.Count > 0)
                ? dto.Horarios
                : (dto.FranjasHorarias != null && dto.FranjasHorarias.Count > 0 ? dto.FranjasHorarias : new List<string> { "10:00" });

            var sedes = (dto.Sedes != null && dto.Sedes.Count > 0)
                ? dto.Sedes
                : (dto.CanchasDisponibles != null && dto.CanchasDisponibles.Count > 0 ? dto.CanchasDisponibles : new List<string> { "Cancha 1" });

            var fechaBase = dto.FechaInicio ?? DateTime.Today.AddDays(1);
            var partidosGenerados = new List<Partido>();

            if (torneo.Modalidad == Modalidad.Liga)
            {
                // Round-Robin (Todos contra todos)
                var teams = new List<Equipo?>(equiposConfirmados);
                if (teams.Count % 2 != 0)
                {
                    teams.Add(null); // Bye
                }

                int n = teams.Count;
                int totalRondas = n - 1;
                int partidosPorRonda = n / 2;

                for (int r = 0; r < totalRondas; r++)
                {
                    // Regla RN-09: Un partido por día calendario por equipo (cada ronda en un día distinto)
                    var fechaRonda = fechaBase.Date.AddDays(r);
                    int partidoIndiceEnRonda = 0;

                    for (int i = 0; i < partidosPorRonda; i++)
                    {
                        var local = teams[i];
                        var visitante = teams[n - 1 - i];

                        if (local != null && visitante != null)
                        {
                            var horarioStr = horarios[partidoIndiceEnRonda % horarios.Count];
                            var sedeStr = sedes[partidoIndiceEnRonda % sedes.Count];
                            var fechaHora = CombinarFechaYHora(fechaRonda, horarioStr, partidoIndiceEnRonda);

                            partidosGenerados.Add(new Partido
                            {
                                Id = Guid.NewGuid(),
                                TorneoId = torneo.Id,
                                EquipoLocalId = local.Id,
                                EquipoVisitanteId = visitante.Id,
                                FechaHora = fechaHora,
                                Sede = sedeStr,
                                Ronda = r + 1,
                                Estado = EstadoPartido.Pendiente
                            });

                            partidoIndiceEnRonda++;
                        }
                    }

                    // Rotación estándar Round-Robin (fija el primero, rota el resto)
                    var ultimo = teams[n - 1];
                    teams.RemoveAt(n - 1);
                    teams.Insert(1, ultimo);
                }
            }
            else
            {
                // Eliminación Directa: cruces de primera ronda
                int cantidad = equiposConfirmados.Count;
                int cruces = cantidad / 2;
                var fechaRonda = fechaBase.Date;

                for (int i = 0; i < cruces; i++)
                {
                    var local = equiposConfirmados[i * 2];
                    var visitante = equiposConfirmados[i * 2 + 1];

                    var horarioStr = horarios[i % horarios.Count];
                    var sedeStr = sedes[i % sedes.Count];
                    var fechaHora = CombinarFechaYHora(fechaRonda, horarioStr, i);

                    partidosGenerados.Add(new Partido
                    {
                        Id = Guid.NewGuid(),
                        TorneoId = torneo.Id,
                        EquipoLocalId = local.Id,
                        EquipoVisitanteId = visitante.Id,
                        FechaHora = fechaHora,
                        Sede = sedeStr,
                        Ronda = 1,
                        Estado = EstadoPartido.Pendiente
                    });
                }
            }

            // RN-08: Cierre definitivo de inscripción del torneo y cambio de estado a "En Desarrollo"
            torneo.Estado = EstadoTorneo.EnDesarrollo;
            await _torneoRepository.UpdateAsync(torneo);
            await _partidoRepository.AddRangeAsync(partidosGenerados);

            var nombresEquipos = equiposConfirmados.ToDictionary(e => e.Id, e => e.Nombre);

            return new GenerarFixtureResponseDTO
            {
                TorneoId = torneo.Id,
                Mensaje = "Fixture generado exitosamente y fase de inscripciones cerrada.",
                EstadoTorneo = torneo.Estado.ToString(),
                TotalPartidosGenerados = partidosGenerados.Count,
                Partidos = partidosGenerados.Select(p => new PartidoResponseDTO
                {
                    Id = p.Id,
                    TorneoId = p.TorneoId,
                    EquipoLocalId = p.EquipoLocalId,
                    EquipoLocalNombre = (p.EquipoLocalId.HasValue && nombresEquipos.TryGetValue(p.EquipoLocalId.Value, out var nLocal)) ? nLocal : "Equipo Local",
                    EquipoVisitanteId = p.EquipoVisitanteId,
                    EquipoVisitanteNombre = (p.EquipoVisitanteId.HasValue && nombresEquipos.TryGetValue(p.EquipoVisitanteId.Value, out var nVis)) ? nVis : "Equipo Visitante",
                    FechaHora = p.FechaHora,
                    Sede = p.Sede,
                    Ronda = p.Ronda,
                    Estado = p.Estado.ToString()
                }).ToList()
            };
        }

        private static DateTime CombinarFechaYHora(DateTime fecha, string horarioStr, int offsetIndice)
        {
            if (TimeSpan.TryParse(horarioStr, out var ts))
            {
                return fecha.Date.Add(ts);
            }
            return fecha.Date.AddHours(9).AddMinutes(offsetIndice * 60);
        }
    }
}
