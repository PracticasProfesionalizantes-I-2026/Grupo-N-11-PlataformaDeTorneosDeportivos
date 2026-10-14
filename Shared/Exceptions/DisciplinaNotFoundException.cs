namespace Shared.Exceptions
{
    public class DisciplinaNotFoundException : NotFoundException
    {
        public DisciplinaNotFoundException(string message = "La disciplina especificada no existe") : base(message)
        {
        }
    }
}
