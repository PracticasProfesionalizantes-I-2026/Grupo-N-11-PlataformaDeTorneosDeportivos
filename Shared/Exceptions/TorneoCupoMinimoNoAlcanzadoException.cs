using System;

namespace Shared.Exceptions
{
    public class TorneoCupoMinimoNoAlcanzadoException : BusinessRuleConflictException
    {
        public TorneoCupoMinimoNoAlcanzadoException(string message = "No se puede generar el fixture. No se alcanza el mínimo de equipos requeridos.")
            : base(message)
        {
        }
    }
}
