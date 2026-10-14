using System;

namespace Shared.Exceptions
{
    public class MinimoJugadoresInsuficienteException : Exception
    {
        public MinimoJugadoresInsuficienteException(string message = "La lista debe contener al menos el número mínimo de jugadores requeridos para la disciplina") 
            : base(message)
        {
        }
    }
}
