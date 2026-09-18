using System;

namespace Shared.Exceptions
{
    public class BusinessRuleConflictException : Exception
    {
        public BusinessRuleConflictException(string message) : base(message) { }
    }
}
