using System;

namespace Shared.Exceptions
{
    public class EquipoNombreDuplicadoException : BusinessRuleConflictException
    {
        public EquipoNombreDuplicadoException(string message = "Ya existe un equipo con ese nombre en este torneo") 
            : base(message)
        {
        }
    }
}
