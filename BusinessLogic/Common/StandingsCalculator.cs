using System;
using System.Collections.Generic;
using System.Linq;
using DataAccess.Entities;
using Shared.DTOs;
using Shared.Enums;

namespace BusinessLogic.Common
{
    public static class StandingsCalculator
    {
        public static List<TablaPosicion> CalculateStandingsEntities(
            Guid torneoId,
            IEnumerable<Equipo>? equipos,
            IEnumerable<Partido>? partidos)
        {
            var dtos = CalculateStandingsDTOs(equipos, partidos);
            return dtos.Select(d => new TablaPosicion
            {
                Id = Guid.NewGuid(),
                TorneoId = torneoId,
                EquipoId = d.EquipoId,
                EquipoNombre = d.EquipoNombre,
                Posicion = d.Posicion,
                PartidosJugados = d.PartidosJugados,
                PartidosGanados = d.PartidosGanados,
                PartidosEmpatados = d.PartidosEmpatados,
                PartidosPerdidos = d.PartidosPerdidos,
                GolesAFavor = d.GolesAFavor,
                GolesEnContra = d.GolesEnContra,
                DiferenciaGoles = d.DiferenciaGoles,
                Puntos = d.Puntos,
                UltimaActualizacion = DateTime.UtcNow
            }).ToList();
        }

        public static List<PosicionTablaDTO> CalculateStandingsDTOs(
            IEnumerable<Equipo>? equipos,
            IEnumerable<Partido>? partidos)
        {
            var equiposList = (equipos != null && equipos.Any(e => e.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado))
                ? equipos.Where(e => e.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado).ToList()
                : (equipos?.ToList() ?? new List<Equipo>());

            var tabla = new Dictionary<Guid, PosicionTablaDTO>();

            foreach (var eq in equiposList)
            {
                tabla[eq.Id] = new PosicionTablaDTO
                {
                    EquipoId = eq.Id,
                    EquipoNombre = eq.Nombre
                };
            }

            var listaPartidos = partidos?.ToList() ?? new List<Partido>();
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

        public static PosicionTablaDTO MapEntityToDTO(TablaPosicion entity)
        {
            return new PosicionTablaDTO
            {
                Posicion = entity.Posicion,
                EquipoId = entity.EquipoId,
                EquipoNombre = entity.EquipoNombre,
                PartidosJugados = entity.PartidosJugados,
                PartidosGanados = entity.PartidosGanados,
                PartidosEmpatados = entity.PartidosEmpatados,
                PartidosPerdidos = entity.PartidosPerdidos,
                GolesAFavor = entity.GolesAFavor,
                GolesEnContra = entity.GolesEnContra,
                DiferenciaGoles = entity.DiferenciaGoles,
                Puntos = entity.Puntos
            };
        }
    }
}

