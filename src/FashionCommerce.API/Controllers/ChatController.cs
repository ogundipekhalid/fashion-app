using Asp.Versioning;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Chat.Commands.AdminReviewFlaggedConversation;
using FashionCommerce.Application.Features.Chat.Commands.MarkMessagesAsRead;
using FashionCommerce.Application.Features.Chat.Commands.ReportConversation;
using FashionCommerce.Application.Features.Chat.Commands.SendMessage;
using FashionCommerce.Application.Features.Chat.Commands.StartConversation;
using FashionCommerce.Application.Features.Chat.Queries.GetConversationMessages;
using FashionCommerce.Application.Features.Chat.Queries.GetMyConversations;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class ChatController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public ChatController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    /// <summary>Customer starts a new product-scoped conversation with the product's vendor.</summary>
    [HttpPost("conversations")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> StartConversation(StartConversationCommand command)
    {
        var customerId = RequireProfileId();
        var result = await Mediator.Send(command with { CustomerId = customerId });
        return FromResult(result, "Conversation started.");
    }

    /// <summary>Lists the current Customer's or Vendor's conversations, most recently active first.</summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> GetMyConversations()
    {
        var role = _currentUser.Role ?? throw new AuthenticationFailedException("Not authenticated.");
        var conversations = await Mediator.Send(new GetMyConversationsQuery(RequireProfileId(), role));
        return Ok(ApiResponse<object>.SuccessResponse(conversations));
    }

    /// <summary>Full message history for a conversation. Blocked messages are only visible to Admin.</summary>
    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid id)
    {
        var role = _currentUser.Role ?? throw new AuthenticationFailedException("Not authenticated.");
        var messages = await Mediator.Send(new GetConversationMessagesQuery(id, RequireProfileId(), role));
        return Ok(ApiResponse<object>.SuccessResponse(messages));
    }

    /// <summary>Sends a message. Every message is checked by ChatModerationService before it's delivered — see SendMessageCommandHandler.</summary>
    [HttpPost("conversations/{id:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid id, SendMessageCommand command)
    {
        var role = _currentUser.Role ?? throw new AuthenticationFailedException("Not authenticated.");
        var result = await Mediator.Send(command with
        {
            ConversationId = id,
            SenderId = RequireProfileId(),
            SenderType = role.ToString()
        });
        return FromResult(result, "Message sent.");
    }

    /// <summary>Either participant reports the conversation for human review (distinct from the automatic content flag).</summary>
    [HttpPost("conversations/{id:guid}/report")]
    public async Task<IActionResult> Report(Guid id, ReportConversationCommand command)
    {
        var result = await Mediator.Send(command with { ConversationId = id, ReportedBy = RequireProfileId() });
        return FromResult(result, "Conversation reported. An admin will review it shortly.");
    }

    /// <summary>Admin resolves an auto-flagged or user-reported conversation.</summary>
    [HttpPost("conversations/{id:guid}/admin-review")]
    [Authorize(Roles = "Admin,SuperAdmin,SupportAgent")]
    public async Task<IActionResult> AdminReview(Guid id, AdminReviewFlaggedConversationCommand command)
    {
        var result = await Mediator.Send(command with { ConversationId = id });
        return FromResult(result, "Conversation review recorded.");
    }

    /// <summary>Marks every unread message in this conversation (not sent by the caller) as read.</summary>
    [HttpPost("conversations/{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var result = await Mediator.Send(new MarkMessagesAsReadCommand(id, RequireProfileId()));
        return FromResult(result);
    }

    private Guid RequireProfileId() => _currentUser.ProfileId ?? throw new AuthenticationFailedException("Not authenticated.");
}
