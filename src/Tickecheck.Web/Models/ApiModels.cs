namespace Tickecheck.Web.Models;

// These types mirror the JSON contract of the API. They are copies, not references to
// the backend projects, on purpose: the frontend is a separate client that only knows
// the HTTP contract, and none of the server code is downloaded to the browser.

public enum TicketStatus
{
    Open,
    InProgress,
    WaitingOnCustomer,
    Resolved,
    Closed,
    Reopened
}

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum TicketChangeType
{
    StatusChanged,
    AssigneeChanged,
    PriorityChanged
}

public enum UserRole
{
    SuperAdmin,
    TenantAdmin,
    Agent,
    Requester
}

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken);

public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalCount);

public record TicketDto(
    Guid Id,
    string Title,
    string Description,
    TicketPriority Priority,
    TicketStatus Status,
    Guid CategoryId,
    string CategoryName,
    Guid RequesterId,
    string RequesterName,
    Guid? AssignedAgentId,
    string? AssignedAgentName,
    DateTime DueAt,
    bool IsOverdue,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt);

public record TicketHistoryDto(
    Guid Id,
    TicketChangeType ChangeType,
    string? OldValue,
    string? NewValue,
    string ChangedByName,
    DateTime OccurredAt);

public record CommentDto(Guid Id, Guid TicketId, Guid AuthorId, string AuthorName, string Body, bool IsInternal, DateTime CreatedAt);
public record CategoryDto(Guid Id, string Name);
public record UserDto(Guid Id, string Email, string FullName, UserRole Role);
public record TenantDto(Guid Id, string Name, DateTime CreatedAt);

public record DashboardStatsDto(
    int TotalTickets,
    int OverdueTickets,
    double? AverageResolutionHours,
    Dictionary<TicketStatus, int> TicketsByStatus,
    Dictionary<TicketPriority, int> TicketsByPriority);

public record CreateTicketRequest(string Title, string Description, TicketPriority Priority, Guid CategoryId);
public record UpdateTicketRequest(string Title, string Description, Guid CategoryId);
public record AssignTicketRequest(Guid AgentId);
public record ChangeStatusRequest(TicketStatus Status);
public record ChangePriorityRequest(TicketPriority Priority);
public record AddCommentRequest(string Body, bool IsInternal);
public record CreateUserRequest(string Email, string FullName, string Password, UserRole Role);
public record CreateCategoryRequest(string Name);
public record CreateTenantRequest(string Name, string AdminEmail, string AdminFullName, string AdminPassword);

// Payloads of the SignalR events.
public record TicketAssignedNotification(Guid TicketId, string Title);
public record CommentAddedNotification(Guid TicketId, string TicketTitle, Guid AuthorId, bool IsInternal);
