using Tickecheck.Application.Abstractions;
using Tickecheck.Domain.Users;

namespace Tickecheck.Application.Common;

/// <summary>
/// Rules about WHICH DATA a user may see inside their own company.
///
/// Role policies on the controllers answer "may this role call this endpoint?".
/// They cannot answer "which rows of the list does this user get?", because that
/// depends on the data. Those rules live here, in one place, instead of being
/// repeated as ifs across the use cases.
/// </summary>
public static class AccessRules
{
    /// <summary>
    /// A Requester only works with their own tickets. Agents and admins see all
    /// tickets of the company (the tenant filter already limits them to it).
    /// </summary>
    public static void EnsureCanAccessTicket(ICurrentUser user, Guid ticketRequesterId)
    {
        if (user.Role == UserRole.Requester && ticketRequesterId != user.UserId)
        {
            // 404 and not 403: the user must not learn that the ticket exists.
            throw new NotFoundException("Ticket not found.");
        }
    }

    /// <summary>
    /// The requester id that list queries must be limited to, or null for no limit.
    /// </summary>
    public static Guid? GetRequesterScope(ICurrentUser user)
    {
        return user.Role == UserRole.Requester ? user.UserId : null;
    }

    /// <summary>
    /// Internal comments are notes between staff and are never shown to a Requester.
    /// </summary>
    public static bool CanSeeInternalComments(ICurrentUser user)
    {
        return user.Role != UserRole.Requester;
    }
}
