namespace Tickecheck.Application.Abstractions;

/// <summary>
/// Saves everything a use case changed in one database transaction.
/// Repositories only track changes; the handler decides when to commit.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
