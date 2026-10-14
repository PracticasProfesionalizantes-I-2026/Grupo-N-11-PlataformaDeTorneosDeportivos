using System;

namespace Shared.Exceptions
{
    public class CupoInvalidoEliminacionException : Exception
    {
        public CupoInvalidoEliminacionException(string message = "Para torneos de eliminación directa el cupo debe ser potencia de 2 y mínimo 4 (RN-04)") : base(message)
        {
        }
    }
}
