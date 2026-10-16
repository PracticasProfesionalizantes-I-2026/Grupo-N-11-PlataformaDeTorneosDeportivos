using System;

namespace Shared.Exceptions
{
    public class ModalidadIncompatibleException : ValidationException
    {
        public ModalidadIncompatibleException(string message = "La vista solicitada no es compatible con el formato del torneo.")
            : base(message)
        {
        }
    }
}
