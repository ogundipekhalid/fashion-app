using System.Security.Claims;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Chat.Commands.MarkMessagesAsRead;
using FashionCommerce.Application.Features.Chat.Commands.SendMessage;
using FashionCommerce.Application.Features.Chat.Queries.GetConversationMessages;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FashionCommerce.API.Hubs;

/// <summary>
/// Thin real-time layer over the Chat module's existing CQRS commands —
/// every method here delegates the actual business logic (including
/// content moderation) to the same handlers the REST ChatController uses,
/// so there's exactly one place message-sending rules live. This class
/// only adds what a Hub uniquely provides: group-based broadcast, typing
/// indicators, and connection-based presence.
///
/// Groups are named "conversation:{conversationId}" — clients call
/// JoinConversation after opening a thread to receive live updates for it.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private const string PresenceGroup = "presence:all";

    private readonly ISender _mediator;
    private readonly IPresenceTracker _presenceTracker;

    public ChatHub(ISender mediator, IPresenceTracker presenceTracker)
    {
        _mediator = mediator;
        _presenceTracker = presenceTracker;
    }

    private Guid ProfileId => Guid.TryParse(Context.User?.FindFirstValue("profile_id"), out var id)
        ? id
        : throw new AuthenticationFailedException("Not authenticated.");

    private UserRole Role => Enum.TryParse<UserRole>(Context.User?.FindFirstValue(ClaimTypes.Role), out var role)
        ? role
        : throw new AuthenticationFailedException("Not authenticated.");

    /// <summary>
    /// Every connection joins a shared presence group so clients receive
    /// UserOnline/UserOffline broadcasts without needing to know in advance
    /// which specific users they care about — the client filters for the
    /// user ids it's actually displaying (e.g. the vendor on the current
    /// product page). Broadcasting only on the FIRST connection for a user
    /// (see IPresenceTracker.UserConnected's return value) means opening a
    /// second tab doesn't spam an extra "online" event.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, PresenceGroup);

        if (_presenceTracker.UserConnected(ProfileId, Context.ConnectionId))
            await Clients.OthersInGroup(PresenceGroup).SendAsync("UserOnline", new { UserId = ProfileId });

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_presenceTracker.UserDisconnected(ProfileId, Context.ConnectionId))
            await Clients.OthersInGroup(PresenceGroup).SendAsync("UserOffline", new { UserId = ProfileId, LastSeenUtc = DateTime.UtcNow });

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Lets a client check a specific user's status on demand (e.g. when first opening a conversation, before any live event has fired).</summary>
    public Task<object> GetOnlineStatus(Guid userId)
    {
        var isOnline = _presenceTracker.IsOnline(userId);
        return Task.FromResult<object>(new { UserId = userId, IsOnline = isOnline, LastSeenUtc = isOnline ? null : _presenceTracker.GetLastSeenUtc(userId) });
    }

    public async Task JoinConversation(Guid conversationId)
    {
        // Access is enforced by re-using GetConversationMessagesQuery's
        // ownership check rather than duplicating it — if the caller isn't a
        // participant, this throws and the client never joins the group.
        await _mediator.Send(new GetConversationMessagesQuery(conversationId, ProfileId, Role));
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(conversationId));
    }

    public async Task LeaveConversation(Guid conversationId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(conversationId));

    /// <summary>
    /// Sends a message through the same SendMessageCommand (and therefore
    /// the same content moderation) the REST endpoint uses, then broadcasts
    /// the result to everyone else in the conversation's group. If the
    /// message was blocked by moderation, only the sender is told — the
    /// other participant never sees a blocked message, on either channel.
    /// </summary>
    public async Task SendMessage(Guid conversationId, string? content, string? attachmentUrl, string type = "Text")
    {
        var result = await _mediator.Send(new SendMessageCommand
        {
            ConversationId = conversationId,
            SenderId = ProfileId,
            SenderType = Role.ToString(),
            Content = content,
            AttachmentUrl = attachmentUrl,
            Type = type
        });

        if (!result.Succeeded)
        {
            await Clients.Caller.SendAsync("MessageBlocked", result.Errors.FirstOrDefault());
            return;
        }

        await Clients.OthersInGroup(GroupName(conversationId)).SendAsync("MessageReceived", new
        {
            MessageId = result.Value,
            ConversationId = conversationId,
            SenderId = ProfileId,
            SenderType = Role.ToString(),
            Content = content,
            AttachmentUrl = attachmentUrl,
            SentAt = DateTime.UtcNow
        });

        await Clients.Caller.SendAsync("MessageSent", new { MessageId = result.Value });
    }

    /// <summary>Ephemeral only — never persisted, matching the spec's "Typing indicator" requirement as a live-only signal.</summary>
    public async Task Typing(Guid conversationId) =>
        await Clients.OthersInGroup(GroupName(conversationId)).SendAsync("UserTyping", new { ConversationId = conversationId, UserId = ProfileId });

    public async Task MarkAsRead(Guid conversationId)
    {
        var result = await _mediator.Send(new MarkMessagesAsReadCommand(conversationId, ProfileId));
        if (result.Succeeded && result.Value > 0)
            await Clients.OthersInGroup(GroupName(conversationId)).SendAsync("MessagesRead", new { ConversationId = conversationId, ReadBy = ProfileId });
    }

    private static string GroupName(Guid conversationId) => $"conversation:{conversationId}";
}
