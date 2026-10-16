using System;

namespace Shared.Exceptions
{
    public class EquipoNotFoundException : NotFoundException
    {
        public EquipoNotFoundException(string message = "El equipo especificado no existe.") 
            : base(message)
        {
        }
    }
}
