using Tickecheck.Domain.Common;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.Domain.Tests.Tickets;

public class TicketStatusTests
{
    private static readonly DateTime Later = TicketFactory.CreatedAt.AddHours(2);

    [Fact]
    public void New_ticket_starts_open_and_without_history()
    {
        var ticket = TicketFactory.Create();

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Null(ticket.AssignedAgentId);
        Assert.Empty(ticket.History);
    }

    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Open, TicketStatus.Closed)]
    [InlineData(TicketStatus.InProgress, TicketStatus.WaitingOnCustomer)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved)]
    [InlineData(TicketStatus.WaitingOnCustomer, TicketStatus.InProgress)]
    [InlineData(TicketStatus.WaitingOnCustomer, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Closed)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Reopened)]
    [InlineData(TicketStatus.Reopened, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Reopened, TicketStatus.Closed)]
    public void Valid_transition_changes_the_status(TicketStatus from, TicketStatus to)
    {
        var ticket = TicketFactory.CreateInStatus(from);

        ticket.ChangeStatus(to, TicketFactory.AgentId, Later);

        Assert.Equal(to, ticket.Status);
    }

    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.Open)]
    [InlineData(TicketStatus.Open, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Open, TicketStatus.Reopened)]
    [InlineData(TicketStatus.Open, TicketStatus.WaitingOnCustomer)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Closed)]
    [InlineData(TicketStatus.WaitingOnCustomer, TicketStatus.Closed)]
    [InlineData(TicketStatus.Resolved, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Reopened, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed, TicketStatus.Reopened)]
    [InlineData(TicketStatus.Closed, TicketStatus.Open)]
    [InlineData(TicketStatus.Closed, TicketStatus.InProgress)]
    public void Invalid_transition_throws_and_keeps_the_status(TicketStatus from, TicketStatus to)
    {
        var ticket = TicketFactory.CreateInStatus(from);
        var historyCount = ticket.History.Count;

        Assert.Throws<DomainException>(() => ticket.ChangeStatus(to, TicketFactory.AgentId, Later));

        Assert.Equal(from, ticket.Status);
        Assert.Equal(historyCount, ticket.History.Count);
    }

    [Fact]
    public void Cannot_start_work_without_an_assigned_agent()
    {
        var ticket = TicketFactory.Create();

        Assert.Throws<DomainException>(
            () => ticket.ChangeStatus(TicketStatus.InProgress, TicketFactory.AgentId, Later));
    }

    [Fact]
    public void Status_change_is_recorded_in_history()
    {
        var ticket = TicketFactory.Create();
        var adminId = Guid.NewGuid();

        ticket.ChangeStatus(TicketStatus.Closed, adminId, Later);

        var entry = Assert.Single(ticket.History);
        Assert.Equal(TicketChangeType.StatusChanged, entry.ChangeType);
        Assert.Equal("Open", entry.OldValue);
        Assert.Equal("Closed", entry.NewValue);
        Assert.Equal(adminId, entry.ChangedByUserId);
        Assert.Equal(Later, entry.OccurredAt);
        Assert.Equal(ticket.Id, entry.TicketId);
    }

    [Fact]
    public void Resolving_sets_the_resolution_date_and_reopening_clears_it()
    {
        var ticket = TicketFactory.CreateInStatus(TicketStatus.InProgress);

        ticket.ChangeStatus(TicketStatus.Resolved, TicketFactory.AgentId, Later);
        Assert.Equal(Later, ticket.ResolvedAt);

        ticket.ChangeStatus(TicketStatus.Reopened, TicketFactory.RequesterId, Later.AddHours(1));
        Assert.Null(ticket.ResolvedAt);
    }

    [Fact]
    public void Closing_sets_the_closing_date()
    {
        var ticket = TicketFactory.CreateInStatus(TicketStatus.Resolved);

        ticket.ChangeStatus(TicketStatus.Closed, TicketFactory.AgentId, Later);

        Assert.Equal(Later, ticket.ClosedAt);
    }
}
