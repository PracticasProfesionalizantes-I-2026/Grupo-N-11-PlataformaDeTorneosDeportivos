using System;

namespace Shared.Exceptions
{
    public class CostoInvalidoException : ValidationException
    {
        public CostoInvalidoException(string message = "El costo debe ser mayor a cero para torneos pagos") : base(message)
        {
        }
    }
}
