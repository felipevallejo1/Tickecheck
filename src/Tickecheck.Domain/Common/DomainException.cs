namespace Tickecheck.Domain.Common;

/// <summary>
/// Thrown when an operation would break a business rule.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
