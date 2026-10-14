using System;

namespace Shared.Exceptions
{
    public class TorneoNotFoundException : NotFoundException
    {
        public TorneoNotFoundException(string message = "El torneo especificado no existe") 
            : base(message)
        {
        }
    }
}
