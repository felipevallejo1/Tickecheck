using Tickecheck.Domain.Common;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.Domain.Tests.Tickets;

public class TicketChangesTests
{
    private static readonly DateTime Later = TicketFactory.CreatedAt.AddHours(1);

    [Fact]
    public void Assigning_sets_the_agent_and_records_history()
    {
        var ticket = TicketFactory.Create();
        var adminId = Guid.NewGuid();

        ticket.AssignTo(TicketFactory.AgentId, adminId, Later);

        Assert.Equal(TicketFactory.AgentId, ticket.AssignedAgentId);
        var entry = Assert.Single(ticket.History);
        Assert.Equal(TicketChangeType.AssigneeChanged, entry.ChangeType);
        Assert.Null(entry.OldValue);
        Assert.Equal(TicketFactory.AgentId.ToString(), entry.NewValue);
        Assert.Equal(adminId, entry.ChangedByUserId);
    }

    [Fact]
    public void Reassigning_records_the_previous_agent()
    {
        var ticket = TicketFactory.Create();
        var secondAgentId = Guid.NewGuid();
        ticket.AssignTo(TicketFactory.AgentId, TicketFactory.AgentId, Later);

        ticket.AssignTo(secondAgentId, TicketFactory.AgentId, Later);

        Assert.Equal(secondAgentId, ticket.AssignedAgentId);
        Assert.Equal(TicketFactory.AgentId.ToString(), ticket.History.Last().OldValue);
    }

    [Fact]
    public void Assigning_the_same_agent_again_adds_no_history()
    {
        var ticket = TicketFactory.Create();
        ticket.AssignTo(TicketFactory.AgentId, TicketFactory.AgentId, Later);

        ticket.AssignTo(TicketFactory.AgentId, TicketFactory.AgentId, Later);

        Assert.Single(ticket.History);
    }

    [Fact]
    public void Changing_priority_records_history()
    {
        var ticket = TicketFactory.Create(TicketPriority.Low);

        ticket.ChangePriority(TicketPriority.Critical, TicketFactory.AgentId, Later);

        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        var entry = Assert.Single(ticket.History);
        Assert.Equal(TicketChangeType.PriorityChanged, entry.ChangeType);
        Assert.Equal("Low", entry.OldValue);
        Assert.Equal("Critical", entry.NewValue);
    }

    [Fact]
    public void Setting_the_same_priority_adds_no_history()
    {
        var ticket = TicketFactory.Create(TicketPriority.High);

        ticket.ChangePriority(TicketPriority.High, TicketFactory.AgentId, Later);

        Assert.Empty(ticket.History);
    }

    [Fact]
    public void Updating_details_changes_them_without_history()
    {
        var ticket = TicketFactory.Create();
        var newCategoryId = Guid.NewGuid();

        ticket.UpdateDetails("  New title  ", "New description", newCategoryId);

        Assert.Equal("New title", ticket.Title);
        Assert.Equal("New description", ticket.Description);
        Assert.Equal(newCategoryId, ticket.CategoryId);
        Assert.Empty(ticket.History);
    }

    [Fact]
    public void A_closed_ticket_cannot_be_modified()
    {
        var ticket = TicketFactory.CreateInStatus(TicketStatus.Closed);

        Assert.Throws<DomainException>(() => ticket.AssignTo(Guid.NewGuid(), TicketFactory.AgentId, Later));
        Assert.Throws<DomainException>(() => ticket.ChangePriority(TicketPriority.Critical, TicketFactory.AgentId, Later));
        Assert.Throws<DomainException>(() => ticket.UpdateDetails("Title", "Description", Guid.NewGuid()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Title_is_required(string title)
    {
        Assert.Throws<DomainException>(
            () => new Ticket(title, "Description", TicketPriority.Low, Guid.NewGuid(), Guid.NewGuid(), Later));
    }
}
