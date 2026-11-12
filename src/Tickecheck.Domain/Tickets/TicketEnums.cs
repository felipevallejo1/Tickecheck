namespace Tickecheck.Domain.Tickets;

public enum TicketStatus
{
    Open = 0,
    InProgress = 1,
    WaitingOnCustomer = 2,
    Resolved = 3,
    Closed = 4,
    Reopened = 5
}

public enum TicketPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum TicketChangeType
{
    StatusChanged = 0,
    AssigneeChanged = 1,
    PriorityChanged = 2
}
