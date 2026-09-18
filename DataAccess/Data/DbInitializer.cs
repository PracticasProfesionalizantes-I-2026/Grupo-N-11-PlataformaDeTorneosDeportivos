using System;
using System.Linq;
using DataAccess.Entities;
using Shared.Enums;

namespace DataAccess.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            context.Database.EnsureCreated();

            if (context.Torneos.Any())
            {
                return;   // DB has been seeded
            }

            var torneos = new Torneo[]
            {
                new Torneo { Id = Guid.NewGuid(), Nombre = "Torneo de Verano", Disciplina = Disciplina.Futbol, Modalidad = Modalidad.Liga, CupoMaximo = 10, CostoInscripcion = 1500m },
                new Torneo { Id = Guid.NewGuid(), Nombre = "Copa E-Sports", Disciplina = Disciplina.ESports, Modalidad = Modalidad.EliminacionDirecta, CupoMaximo = 8, CostoInscripcion = 500m }
            };

            foreach (var t in torneos)
            {
                context.Torneos.Add(t);
            }
            context.SaveChanges();

            var equipos = new Equipo[]
            {
                new Equipo { Id = Guid.NewGuid(), Nombre = "Los Leones", EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado, TorneoId = torneos[0].Id },
                new Equipo { Id = Guid.NewGuid(), Nombre = "Pixel Warriors", EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion, TorneoId = torneos[1].Id }
            };

            foreach (var e in equipos)
            {
                context.Equipos.Add(e);
            }
            context.SaveChanges();

            var jugadores = new Jugador[]
            {
                new Jugador { Id = Guid.NewGuid(), Nombre = "Juan", Apellido = "Perez", Dni = "12345678", EquipoId = equipos[0].Id },
                new Jugador { Id = Guid.NewGuid(), Nombre = "Martin", Apellido = "Gomez", Dni = "87654321", EquipoId = equipos[1].Id }
            };

            foreach (var j in jugadores)
            {
                context.Jugadores.Add(j);
            }
            context.SaveChanges();
        }
    }
}
