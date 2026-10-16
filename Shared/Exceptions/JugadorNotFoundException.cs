using System;

namespace Shared.Exceptions
{
    public class JugadorNotFoundException : NotFoundException
    {
        public JugadorNotFoundException(string message = "El jugador especificado no existe.") 
            : base(message)
        {
        }
    }
}
