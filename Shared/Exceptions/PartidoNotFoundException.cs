using System;

namespace Shared.Exceptions
{
    public class PartidoNotFoundException : NotFoundException
    {
        public PartidoNotFoundException(string message = "El partido especificado no existe") 
            : base(message)
        {
        }
    }
}
