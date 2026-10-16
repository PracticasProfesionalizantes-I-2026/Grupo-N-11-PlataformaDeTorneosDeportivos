using System;

namespace Shared.Exceptions
{
    public class EquiposInsuficientesException : Exception
    {
        public EquiposInsuficientesException(string message = "Se requieren al menos 4 equipos confirmados para generar el fixture")
            : base(message)
        {
        }
    }
}
