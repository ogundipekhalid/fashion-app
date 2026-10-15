namespace FashionCommerce.Application.Features.Refunds.DTOs;

public record RefundEvidenceDto(Guid Id, string Url, bool IsVideo, string UploadedByRole);

public record RefundAuditLogDto(string Actor, string Message, DateTime Timestamp);

public record RefundRequestDto(
    Guid Id, Guid OrderItemId, Guid OrderId, string ProductName,
    string Type, decimal RequestedAmount, decimal? ApprovedAmount,
    string Reason, string? CustomerNote, string? VendorResponseNote, string? AdminDecisionNote,
    string Status, bool WasVendorAlreadyPaidOut,
    IReadOnlyList<RefundEvidenceDto> Evidence, IReadOnlyList<RefundAuditLogDto> AuditLogs,
    DateTime CreatedAt);
