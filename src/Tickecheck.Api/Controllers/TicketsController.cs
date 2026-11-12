using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tickecheck.Api.Auth;
using Tickecheck.Application.Comments;
using Tickecheck.Application.Common;
using Tickecheck.Application.Tickets;
using Tickecheck.Domain.Tickets;

namespace Tickecheck.Api.Controllers;

// Request bodies. The ticket id always comes from the URL, never from the body.
public record UpdateTicketRequest(string Title, string Description, Guid CategoryId);
public record AssignTicketRequest(Guid AgentId);
public record ChangeStatusRequest(TicketStatus Status);
public record ChangePriorityRequest(TicketPriority Priority);
public record AddCommentRequest(string Body, bool IsInternal);

/// <summary>
/// Every action does the same three things: read the request, call one use case,
/// return its result. There is no business logic here.
/// </summary>
[Authorize(Policy = Policies.TenantMember)]
public class TicketsController : ApiController
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<TicketDto>>> Search(
        [FromQuery] SearchTicketsQuery query,
        [FromServices] SearchTicketsHandler handler,
        CancellationToken cancellationToken)
    {
        return await handler.HandleAsync(query, cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketDto>> GetById(
        Guid id,
        [FromServices] GetTicketByIdHandler handler,
        CancellationToken cancellationToken)
    {
        return await handler.HandleAsync(id, cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<TicketDto>> Create(
        CreateTicketCommand command,
        [FromServices] CreateTicketHandler handler,
        CancellationToken cancellationToken)
    {
        var ticket = await handler.HandleAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TicketDto>> Update(
        Guid id,
        UpdateTicketRequest request,
        [FromServices] UpdateTicketHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateTicketCommand(id, request.Title, request.Description, request.CategoryId);
        return await handler.HandleAsync(command, cancellationToken);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.TenantAdminOnly)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] DeleteTicketHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/assignee")]
    [Authorize(Policy = Policies.AgentOrAbove)]
    public async Task<ActionResult<TicketDto>> Assign(
        Guid id,
        AssignTicketRequest request,
        [FromServices] AssignTicketHandler handler,
        CancellationToken cancellationToken)
    {
        return await handler.HandleAsync(new AssignTicketCommand(id, request.AgentId), cancellationToken);
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = Policies.AgentOrAbove)]
    public async Task<ActionResult<TicketDto>> ChangeStatus(
        Guid id,
        ChangeStatusRequest request,
        [FromServices] ChangeTicketStatusHandler handler,
        CancellationToken cancellationToken)
    {
        return await handler.HandleAsync(new ChangeTicketStatusCommand(id, request.Status), cancellationToken);
    }

    // Open to requesters too: they can reopen their own resolved tickets.
    [HttpPost("{id:guid}/reopen")]
    public async Task<ActionResult<TicketDto>> Reopen(
        Guid id,
        [FromServices] ReopenTicketHandler handler,
        CancellationToken cancellationToken)
    {
        return await handler.HandleAsync(id, cancellationToken);
    }

    [HttpPut("{id:guid}/priority")]
    [Authorize(Policy = Policies.AgentOrAbove)]
    public async Task<ActionResult<TicketDto>> ChangePriority(
        Guid id,
        ChangePriorityRequest request,
        [FromServices] ChangeTicketPriorityHandler handler,
        CancellationToken cancellationToken)
    {
        return await handler.HandleAsync(new ChangeTicketPriorityCommand(id, request.Priority), cancellationToken);
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<List<TicketHistoryDto>>> GetHistory(
        Guid id,
        [FromServices] GetTicketHistoryHandler handler,
        CancellationToken cancellationToken)
    {
        return await handler.HandleAsync(id, cancellationToken);
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<ActionResult<List<CommentDto>>> GetComments(
        Guid id,
        [FromServices] GetCommentsHandler handler,
        CancellationToken cancellationToken)
    {
        return await handler.HandleAsync(id, cancellationToken);
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<CommentDto>> AddComment(
        Guid id,
        AddCommentRequest request,
        [FromServices] AddCommentHandler handler,
        CancellationToken cancellationToken)
    {
        var comment = await handler.HandleAsync(new AddCommentCommand(id, request.Body, request.IsInternal), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, comment);
    }
}
