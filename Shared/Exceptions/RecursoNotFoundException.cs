using System;

namespace Shared.Exceptions
{
    public class RecursoNotFoundException : NotFoundException
    {
        public RecursoNotFoundException(string message = "El recurso solicitado no fue encontrado") 
            : base(message)
        {
        }
    }
}
