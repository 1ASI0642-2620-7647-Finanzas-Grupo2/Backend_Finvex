namespace Finvex.Domain;

public sealed class CreditLimitExceededException : DomainException
{
    public CreditLimitExceededException(decimal requested, decimal available)
        : base($"El monto solicitado ({requested:C}) excede el crédito disponible ({available:C}).") { }
}

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
