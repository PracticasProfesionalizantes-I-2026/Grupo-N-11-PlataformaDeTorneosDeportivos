namespace Shared.Exceptions
{
    public class TorneoNombreDuplicadoException : BusinessRuleConflictException
    {
        public TorneoNombreDuplicadoException(string message = "Ya existe un torneo con el nombre ingresado") : base(message)
        {
        }
    }
}
