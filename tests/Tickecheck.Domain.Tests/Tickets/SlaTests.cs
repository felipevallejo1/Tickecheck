using Tickecheck.Domain.Tickets;

namespace Tickecheck.Domain.Tests.Tickets;

public class SlaTests
{
    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public void Due_date_depends_on_priority(TicketPriority priority, int expectedHours)
    {
        var ticket = TicketFactory.Create(priority);

        Assert.Equal(TicketFactory.CreatedAt.AddHours(expectedHours), ticket.DueAt);
    }

    [Fact]
    public void Unknown_priority_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SlaPolicy.GetResolutionTime((TicketPriority)99));
    }

    [Fact]
    public void Changing_priority_recalculates_the_due_date_from_creation()
    {
        var ticket = TicketFactory.Create(TicketPriority.Low);
        var threeHoursLater = TicketFactory.CreatedAt.AddHours(3);

        ticket.ChangePriority(TicketPriority.Critical, TicketFactory.AgentId, threeHoursLater);

        // 4 hours from creation, not from the moment the priority changed.
        Assert.Equal(TicketFactory.CreatedAt.AddHours(4), ticket.DueAt);
    }

    [Fact]
    public void Ticket_is_not_overdue_before_or_exactly_at_the_due_date()
    {
        var ticket = TicketFactory.Create(TicketPriority.High);

        Assert.False(ticket.IsOverdue(ticket.DueAt.AddMinutes(-1)));
        Assert.False(ticket.IsOverdue(ticket.DueAt));
    }

    [Fact]
    public void Unfinished_ticket_is_overdue_after_the_due_date()
    {
        var ticket = TicketFactory.CreateInStatus(TicketStatus.InProgress);

        Assert.True(ticket.IsOverdue(ticket.DueAt.AddMinutes(1)));
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void Finished_ticket_is_never_overdue(TicketStatus status)
    {
        var ticket = TicketFactory.CreateInStatus(status);

        Assert.False(ticket.IsOverdue(ticket.DueAt.AddDays(30)));
    }

    [Fact]
    public void Reopened_ticket_can_be_overdue_again()
    {
        var ticket = TicketFactory.CreateInStatus(TicketStatus.Reopened);

        Assert.True(ticket.IsOverdue(ticket.DueAt.AddMinutes(1)));
    }
}
