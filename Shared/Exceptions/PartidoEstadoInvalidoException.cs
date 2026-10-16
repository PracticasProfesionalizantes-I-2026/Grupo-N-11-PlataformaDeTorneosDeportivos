using System;

namespace Shared.Exceptions
{
    public class PartidoEstadoInvalidoException : BusinessRuleConflictException
    {
        public PartidoEstadoInvalidoException(string message = "El partido no admite carga de resultados en su estado actual.") 
            : base(message)
        {
        }
    }
}
