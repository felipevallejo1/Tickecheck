using FluentValidation;
using Moq;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;
using Tickecheck.Application.Tickets;
using Tickecheck.Domain.Common;
using Tickecheck.Domain.Tickets;
using Tickecheck.Domain.Users;

namespace Tickecheck.Application.Tests.Tickets;

public class TicketHandlerTests
{
    private readonly Mock<ITicketRepository> _tickets = new();
    private readonly Mock<ICategoryRepository> _categories = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ITicketNotifier> _notifier = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly TimeProvider _clock = TestSupport.Clock();

    public TicketHandlerTests()
    {
        // Handlers return the read model after saving; any DTO is enough for these tests.
        _tickets.Setup(repo => repo.GetDetailsAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync(new TicketDto());
    }

    // ---------- Create ----------

    [Fact]
    public async Task Create_uses_the_current_user_as_requester_and_saves()
    {
        var requester = TestSupport.CurrentUser(UserRole.Requester);
        var categoryId = Guid.NewGuid();
        _categories.Setup(repo => repo.ExistsAsync(categoryId, default)).ReturnsAsync(true);
        var handler = new CreateTicketHandler(_tickets.Object, _categories.Object, requester, _unitOfWork.Object, _clock);

        await handler.HandleAsync(new CreateTicketCommand("No sound", "Speakers are silent.", TicketPriority.High, categoryId));

        _tickets.Verify(repo => repo.Add(It.Is<Ticket>(ticket =>
            ticket.RequesterId == requester.UserId
            && ticket.Status == TicketStatus.Open
            && ticket.CreatedAt == TestSupport.Now
            && ticket.DueAt == TestSupport.Now.AddHours(8))), Times.Once);
        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Create_with_a_category_that_is_not_visible_fails_validation()
    {
        _categories.Setup(repo => repo.ExistsAsync(It.IsAny<Guid>(), default)).ReturnsAsync(false);
        var handler = new CreateTicketHandler(
            _tickets.Object, _categories.Object, TestSupport.CurrentUser(UserRole.Requester), _unitOfWork.Object, _clock);

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.HandleAsync(new CreateTicketCommand("No sound", "Speakers are silent.", TicketPriority.High, Guid.NewGuid())));

        _tickets.Verify(repo => repo.Add(It.IsAny<Ticket>()), Times.Never);
    }

    [Theory]
    [InlineData("", "Description")]
    [InlineData("Title", "")]
    public async Task Create_with_empty_fields_fails_validation(string title, string description)
    {
        var handler = new CreateTicketHandler(
            _tickets.Object, _categories.Object, TestSupport.CurrentUser(UserRole.Requester), _unitOfWork.Object, _clock);

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.HandleAsync(new CreateTicketCommand(title, description, TicketPriority.Low, Guid.NewGuid())));
    }

    // ---------- Read ----------

    [Fact]
    public async Task Get_by_id_hides_the_ticket_of_another_requester_as_not_found()
    {
        var ticketId = Guid.NewGuid();
        _tickets.Setup(repo => repo.GetDetailsAsync(ticketId, It.IsAny<DateTime>(), default))
            .ReturnsAsync(new TicketDto { Id = ticketId, RequesterId = Guid.NewGuid() });
        var handler = new GetTicketByIdHandler(_tickets.Object, TestSupport.CurrentUser(UserRole.Requester), _clock);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(ticketId));
    }

    [Fact]
    public async Task Get_by_id_returns_any_ticket_of_the_company_to_staff()
    {
        var ticketId = Guid.NewGuid();
        _tickets.Setup(repo => repo.GetDetailsAsync(ticketId, It.IsAny<DateTime>(), default))
            .ReturnsAsync(new TicketDto { Id = ticketId, RequesterId = Guid.NewGuid() });
        var handler = new GetTicketByIdHandler(_tickets.Object, TestSupport.CurrentUser(UserRole.Agent), _clock);

        var ticket = await handler.HandleAsync(ticketId);

        Assert.Equal(ticketId, ticket.Id);
    }

    [Fact]
    public async Task Get_by_id_of_a_missing_ticket_is_not_found()
    {
        _tickets.Setup(repo => repo.GetDetailsAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync((TicketDto?)null);
        var handler = new GetTicketByIdHandler(_tickets.Object, TestSupport.CurrentUser(UserRole.Agent), _clock);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Search_by_a_requester_is_limited_to_their_own_tickets()
    {
        var requester = TestSupport.CurrentUser(UserRole.Requester);
        var handler = new SearchTicketsHandler(_tickets.Object, requester, _clock);
        var query = new SearchTicketsQuery();

        await handler.HandleAsync(query);

        _tickets.Verify(repo => repo.SearchAsync(query, requester.UserId, TestSupport.Now, default), Times.Once);
    }

    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.TenantAdmin)]
    public async Task Search_by_staff_has_no_requester_limit(UserRole role)
    {
        var handler = new SearchTicketsHandler(_tickets.Object, TestSupport.CurrentUser(role), _clock);
        var query = new SearchTicketsQuery();

        await handler.HandleAsync(query);

        _tickets.Verify(repo => repo.SearchAsync(query, null, TestSupport.Now, default), Times.Once);
    }

    [Fact]
    public async Task Search_rejects_page_sizes_over_the_limit_and_unknown_sort_fields()
    {
        var handler = new SearchTicketsHandler(_tickets.Object, TestSupport.CurrentUser(UserRole.Agent), _clock);

        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new SearchTicketsQuery { PageSize = 101 }));
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(new SearchTicketsQuery { SortBy = "passwordHash" }));
    }

    // ---------- Update and delete ----------

    [Fact]
    public async Task Update_changes_the_ticket_and_saves()
    {
        var requester = TestSupport.CurrentUser(UserRole.Requester);
        var ticket = StoreTicket(requesterId: requester.UserId);
        var newCategoryId = Guid.NewGuid();
        _categories.Setup(repo => repo.ExistsAsync(newCategoryId, default)).ReturnsAsync(true);
        var handler = new UpdateTicketHandler(_tickets.Object, _categories.Object, requester, _unitOfWork.Object, _clock);

        await handler.HandleAsync(new UpdateTicketCommand(ticket.Id, "New title", "New description", newCategoryId));

        Assert.Equal("New title", ticket.Title);
        Assert.Equal(newCategoryId, ticket.CategoryId);
        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Update_of_another_requesters_ticket_is_not_found()
    {
        var ticket = StoreTicket();
        var handler = new UpdateTicketHandler(
            _tickets.Object, _categories.Object, TestSupport.CurrentUser(UserRole.Requester), _unitOfWork.Object, _clock);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.HandleAsync(new UpdateTicketCommand(ticket.Id, "New title", "New description", Guid.NewGuid())));

        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Delete_removes_the_ticket()
    {
        var ticket = StoreTicket();
        var handler = new DeleteTicketHandler(_tickets.Object, _unitOfWork.Object);

        await handler.HandleAsync(ticket.Id);

        _tickets.Verify(repo => repo.Remove(ticket), Times.Once);
        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Delete_of_a_missing_ticket_is_not_found()
    {
        var handler = new DeleteTicketHandler(_tickets.Object, _unitOfWork.Object);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(Guid.NewGuid()));
    }

    // ---------- Assign ----------

    [Fact]
    public async Task Assign_sets_the_agent_writes_history_and_notifies()
    {
        var admin = TestSupport.CurrentUser(UserRole.TenantAdmin);
        var ticket = StoreTicket();
        var agent = StoreUser(UserRole.Agent);
        var handler = NewAssignHandler(admin);

        await handler.HandleAsync(new AssignTicketCommand(ticket.Id, agent.Id));

        Assert.Equal(agent.Id, ticket.AssignedAgentId);
        var entry = Assert.Single(ticket.History);
        Assert.Equal(admin.UserId, entry.ChangedByUserId);
        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Once);
        _notifier.Verify(notifier => notifier.TicketAssignedAsync(ticket, default), Times.Once);
    }

    [Fact]
    public async Task Assigning_the_same_agent_again_does_not_notify()
    {
        var ticket = StoreTicket();
        var agent = StoreUser(UserRole.Agent);
        ticket.AssignTo(agent.Id, agent.Id, TestSupport.Now);
        var handler = NewAssignHandler(TestSupport.CurrentUser(UserRole.TenantAdmin));

        await handler.HandleAsync(new AssignTicketCommand(ticket.Id, agent.Id));

        _notifier.Verify(notifier => notifier.TicketAssignedAsync(It.IsAny<Ticket>(), default), Times.Never);
    }

    [Fact]
    public async Task Assign_to_a_requester_fails_validation()
    {
        var ticket = StoreTicket();
        var requester = StoreUser(UserRole.Requester);
        var handler = NewAssignHandler(TestSupport.CurrentUser(UserRole.TenantAdmin));

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.HandleAsync(new AssignTicketCommand(ticket.Id, requester.Id)));

        Assert.Null(ticket.AssignedAgentId);
    }

    [Fact]
    public async Task Assign_to_a_user_that_is_not_visible_fails_validation()
    {
        // The repository is tenant-filtered, so an agent of another company comes back as null.
        var ticket = StoreTicket();
        var handler = NewAssignHandler(TestSupport.CurrentUser(UserRole.TenantAdmin));

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.HandleAsync(new AssignTicketCommand(ticket.Id, Guid.NewGuid())));
    }

    [Fact]
    public async Task Assign_of_a_missing_ticket_is_not_found()
    {
        var handler = NewAssignHandler(TestSupport.CurrentUser(UserRole.TenantAdmin));

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.HandleAsync(new AssignTicketCommand(Guid.NewGuid(), Guid.NewGuid())));
    }

    // ---------- Status and priority ----------

    [Fact]
    public async Task Change_status_applies_a_valid_transition_and_saves()
    {
        var agent = TestSupport.CurrentUser(UserRole.Agent);
        var ticket = StoreTicket();
        ticket.AssignTo(agent.UserId, agent.UserId, TestSupport.Now);
        var handler = new ChangeTicketStatusHandler(_tickets.Object, agent, _unitOfWork.Object, _clock);

        await handler.HandleAsync(new ChangeTicketStatusCommand(ticket.Id, TicketStatus.InProgress));

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Change_status_lets_the_domain_exception_through_and_does_not_save()
    {
        var ticket = StoreTicket();
        var handler = new ChangeTicketStatusHandler(
            _tickets.Object, TestSupport.CurrentUser(UserRole.Agent), _unitOfWork.Object, _clock);

        await Assert.ThrowsAsync<DomainException>(
            () => handler.HandleAsync(new ChangeTicketStatusCommand(ticket.Id, TicketStatus.Resolved)));

        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Reopen_is_allowed_to_the_requester_of_the_ticket()
    {
        var requester = TestSupport.CurrentUser(UserRole.Requester);
        var ticket = StoreResolvedTicket(requester.UserId);
        var handler = new ReopenTicketHandler(_tickets.Object, requester, _unitOfWork.Object, _clock);

        await handler.HandleAsync(ticket.Id);

        Assert.Equal(TicketStatus.Reopened, ticket.Status);
        Assert.Equal(requester.UserId, ticket.History.Last().ChangedByUserId);
    }

    [Fact]
    public async Task Reopen_of_another_requesters_ticket_is_not_found()
    {
        var ticket = StoreResolvedTicket(requesterId: Guid.NewGuid());
        var handler = new ReopenTicketHandler(
            _tickets.Object, TestSupport.CurrentUser(UserRole.Requester), _unitOfWork.Object, _clock);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(ticket.Id));

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
    }

    [Fact]
    public async Task Change_priority_updates_the_deadline_and_saves()
    {
        var ticket = StoreTicket();
        var handler = new ChangeTicketPriorityHandler(
            _tickets.Object, TestSupport.CurrentUser(UserRole.Agent), _unitOfWork.Object, _clock);

        await handler.HandleAsync(new ChangeTicketPriorityCommand(ticket.Id, TicketPriority.Critical));

        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Equal(ticket.CreatedAt.AddHours(4), ticket.DueAt);
        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task History_of_another_requesters_ticket_is_not_found()
    {
        var ticket = StoreTicket();
        var handler = new GetTicketHistoryHandler(_tickets.Object, TestSupport.CurrentUser(UserRole.Requester));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(ticket.Id));

        _tickets.Verify(repo => repo.GetHistoryAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    // ---------- Helpers ----------

    private AssignTicketHandler NewAssignHandler(ICurrentUser currentUser)
    {
        return new AssignTicketHandler(_tickets.Object, _users.Object, currentUser, _notifier.Object, _unitOfWork.Object, _clock);
    }

    private Ticket StoreTicket(Guid? requesterId = null)
    {
        var ticket = TestSupport.NewTicket(requesterId);
        _tickets.Setup(repo => repo.GetByIdAsync(ticket.Id, default)).ReturnsAsync(ticket);
        return ticket;
    }

    private Ticket StoreResolvedTicket(Guid requesterId)
    {
        var agentId = Guid.NewGuid();
        var ticket = StoreTicket(requesterId);
        ticket.AssignTo(agentId, agentId, TestSupport.Now);
        ticket.ChangeStatus(TicketStatus.InProgress, agentId, TestSupport.Now);
        ticket.ChangeStatus(TicketStatus.Resolved, agentId, TestSupport.Now);
        return ticket;
    }

    private User StoreUser(UserRole role)
    {
        var user = TestSupport.NewUser(role);
        _users.Setup(repo => repo.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        return user;
    }
}
