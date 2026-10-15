namespace FashionCommerce.Application.Features.Support.DTOs;

public record SupportTicketMessageDto(Guid Id, Guid SenderId, string SenderRole, string Content, DateTime SentAt);

public record SupportTicketDto(
    Guid Id, string TicketNumber, string Category, string Priority, string Status,
    string Subject, string Description, Guid? AssignedAgentId,
    DateTime CreatedAt, DateTime? ResolvedAt, string? ResolutionNote,
    IReadOnlyList<SupportTicketMessageDto> Messages);

public record SupportTicketSummaryDto(Guid Id, string TicketNumber, string Category, string Priority, string Status, string Subject, DateTime CreatedAt);

public record BotMessageDto(string Sender, string Content, string? Intent, DateTime SentAt);

public record BotConversationDto(Guid Id, string Channel, bool Ended, Guid? EscalatedToSupportTicketId, IReadOnlyList<BotMessageDto> Messages);
