namespace Tickecheck.Infrastructure.Persistence;

/// <summary>
/// Thrown when a save would write data outside the current tenant.
/// It always means a bug in our code, never a user mistake, so it surfaces as a 500.
/// </summary>
public class TenantViolationException : InvalidOperationException
{
    public TenantViolationException(string message) : base(message)
    {
    }
}
