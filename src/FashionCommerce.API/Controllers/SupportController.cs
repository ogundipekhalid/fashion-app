using Asp.Versioning;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Support.Commands.AddSupportTicketMessage;
using FashionCommerce.Application.Features.Support.Commands.AssignSupportTicket;
using FashionCommerce.Application.Features.Support.Commands.CreateSupportTicket;
using FashionCommerce.Application.Features.Support.Commands.ResolveSupportTicket;
using FashionCommerce.Application.Features.Support.Commands.SendBotMessage;
using FashionCommerce.Application.Features.Support.Commands.StartBotConversation;
using FashionCommerce.Application.Features.Support.Queries.GetMySupportTickets;
using FashionCommerce.Application.Features.Support.Queries.GetSupportTicketById;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

/// <summary>
/// Two related but distinct paths, both answering "how do I report an
/// issue / reach the developer": a direct ticket form (POST tickets), and
/// a conversational bot (POST bot/start, POST bot/{id}/messages) that can
/// escalate itself into a ticket automatically.
/// </summary>
[ApiVersion("1.0")]
[Authorize]
public class SupportController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public SupportController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket(CreateSupportTicketCommand command)
    {
        var result = await Mediator.Send(command with { RequesterId = RequireProfileId(), RequesterRole = _currentUser.Role!.Value.ToString() });
        return FromResult(result, "Support ticket created. Our team will follow up shortly.");
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> GetMyTickets([FromQuery] bool staffViewAll = false, [FromQuery] string? statusFilter = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetMySupportTicketsQuery
        {
            RequesterId = RequireProfileId(),
            RequesterRole = _currentUser.Role!.Value,
            StaffViewAll = staffViewAll,
            StatusFilter = statusFilter,
            PageNumber = pageNumber,
            PageSize = pageSize
        });

        return Ok(new PaginatedApiResponse<object>
        {
            Success = true,
            Data = result.Items.Cast<object>(),
            PageNumber = result.PageNumber,
            TotalPages = result.TotalPages,
            TotalCount = result.TotalCount,
            HasPreviousPage = result.HasPreviousPage,
            HasNextPage = result.HasNextPage
        });
    }

    [HttpGet("tickets/{id:guid}")]
    public async Task<IActionResult> GetTicketById(Guid id)
    {
        var ticket = await Mediator.Send(new GetSupportTicketByIdQuery(id, RequireProfileId(), _currentUser.Role!.Value));
        return Ok(ApiResponse<object>.SuccessResponse(ticket));
    }

    [HttpPost("tickets/{id:guid}/messages")]
    public async Task<IActionResult> AddTicketMessage(Guid id, AddSupportTicketMessageCommand command)
    {
        var result = await Mediator.Send(command with { SupportTicketId = id, SenderId = RequireProfileId(), SenderRole = _currentUser.Role!.Value.ToString() });
        return FromResult(result, "Message added.");
    }

    [HttpPost("tickets/{id:guid}/resolve")]
    [Authorize(Roles = "Admin,SuperAdmin,SupportAgent")]
    public async Task<IActionResult> ResolveTicket(Guid id, ResolveSupportTicketCommand command)
    {
        var result = await Mediator.Send(command with { SupportTicketId = id });
        return FromResult(result, "Ticket resolved.");
    }

    [HttpPost("tickets/{id:guid}/assign")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> AssignTicket(Guid id, AssignSupportTicketCommand command)
    {
        var result = await Mediator.Send(command with { SupportTicketId = id });
        return FromResult(result, "Ticket assigned.");
    }

    /// <summary>Starts a new bot conversation. Channel: "DeveloperContactBot", "CustomerSupportBot", or "AiStylingAssistant".</summary>
    [HttpPost("bot/start")]
    public async Task<IActionResult> StartBotConversation(StartBotConversationCommand command)
    {
        var result = await Mediator.Send(command with { UserId = RequireProfileId(), UserRole = _currentUser.Role!.Value.ToString() });
        return FromResult(result, "Conversation started.");
    }

    [HttpPost("bot/{id:guid}/messages")]
    public async Task<IActionResult> SendBotMessage(Guid id, SendBotMessageCommand command)
    {
        var result = await Mediator.Send(command with { BotConversationId = id });
        return FromResult(result);
    }

    private Guid RequireProfileId() => _currentUser.ProfileId ?? throw new AuthenticationFailedException("Not authenticated.");
}
