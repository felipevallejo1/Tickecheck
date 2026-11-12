using FluentValidation;
using Moq;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Comments;
using Tickecheck.Application.Common;
using Tickecheck.Domain.Tickets;
using Tickecheck.Domain.Users;

namespace Tickecheck.Application.Tests.Comments;

public class CommentHandlerTests
{
    private readonly Mock<ITicketRepository> _tickets = new();
    private readonly Mock<ICommentRepository> _comments = new();
    private readonly Mock<ITicketNotifier> _notifier = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task Requester_adds_a_public_comment_to_their_own_ticket()
    {
        var requester = TestSupport.CurrentUser(UserRole.Requester);
        var ticket = StoreTicket(requester.UserId);

        var comment = await NewAddHandler(requester).HandleAsync(new AddCommentCommand(ticket.Id, "Any news?", false));

        Assert.Equal("Any news?", comment.Body);
        Assert.Equal(requester.UserId, comment.AuthorId);
        Assert.Equal(TestSupport.Now, comment.CreatedAt);
        _comments.Verify(repo => repo.Add(It.Is<Comment>(c => c.TicketId == ticket.Id && !c.IsInternal)), Times.Once);
        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Once);
        _notifier.Verify(notifier => notifier.CommentAddedAsync(ticket, It.IsAny<Comment>(), default), Times.Once);
    }

    [Fact]
    public async Task Requester_cannot_write_an_internal_comment()
    {
        var requester = TestSupport.CurrentUser(UserRole.Requester);
        var ticket = StoreTicket(requester.UserId);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => NewAddHandler(requester).HandleAsync(new AddCommentCommand(ticket.Id, "Secret", true)));

        _comments.Verify(repo => repo.Add(It.IsAny<Comment>()), Times.Never);
        _notifier.Verify(notifier => notifier.CommentAddedAsync(It.IsAny<Ticket>(), It.IsAny<Comment>(), default), Times.Never);
    }

    [Fact]
    public async Task Requester_cannot_comment_on_the_ticket_of_someone_else()
    {
        var ticket = StoreTicket(requesterId: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(
            () => NewAddHandler(TestSupport.CurrentUser(UserRole.Requester))
                .HandleAsync(new AddCommentCommand(ticket.Id, "Hello", false)));
    }

    [Fact]
    public async Task Agent_adds_an_internal_comment()
    {
        var ticket = StoreTicket(requesterId: Guid.NewGuid());

        var comment = await NewAddHandler(TestSupport.CurrentUser(UserRole.Agent))
            .HandleAsync(new AddCommentCommand(ticket.Id, "Checked the logs.", true));

        Assert.True(comment.IsInternal);
    }

    [Fact]
    public async Task Empty_comment_fails_validation()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => NewAddHandler(TestSupport.CurrentUser(UserRole.Agent))
                .HandleAsync(new AddCommentCommand(Guid.NewGuid(), "", false)));
    }

    [Theory]
    [InlineData(UserRole.Requester, false)]
    [InlineData(UserRole.Agent, true)]
    [InlineData(UserRole.TenantAdmin, true)]
    public async Task Listing_includes_internal_comments_only_for_staff(UserRole role, bool expectedIncludeInternal)
    {
        var user = TestSupport.CurrentUser(role);
        var ticket = StoreTicket(user.UserId);
        _comments.Setup(repo => repo.ListByTicketAsync(ticket.Id, It.IsAny<bool>(), default)).ReturnsAsync([]);
        var handler = new GetCommentsHandler(_tickets.Object, _comments.Object, user);

        await handler.HandleAsync(ticket.Id);

        _comments.Verify(repo => repo.ListByTicketAsync(ticket.Id, expectedIncludeInternal, default), Times.Once);
    }

    [Fact]
    public async Task Listing_comments_of_another_requesters_ticket_is_not_found()
    {
        var ticket = StoreTicket(requesterId: Guid.NewGuid());
        var handler = new GetCommentsHandler(_tickets.Object, _comments.Object, TestSupport.CurrentUser(UserRole.Requester));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(ticket.Id));
    }

    private AddCommentHandler NewAddHandler(ICurrentUser currentUser)
    {
        return new AddCommentHandler(
            _tickets.Object, _comments.Object, currentUser, _notifier.Object, _unitOfWork.Object, TestSupport.Clock());
    }

    private Ticket StoreTicket(Guid requesterId)
    {
        var ticket = TestSupport.NewTicket(requesterId);
        _tickets.Setup(repo => repo.GetByIdAsync(ticket.Id, default)).ReturnsAsync(ticket);
        return ticket;
    }
}
