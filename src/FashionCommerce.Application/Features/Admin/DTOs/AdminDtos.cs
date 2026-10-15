namespace FashionCommerce.Application.Features.Admin.DTOs;

public record DashboardSummaryDto(
    decimal TotalRevenue, int TotalOrders, int TotalCustomers, int TotalVendors,
    int PendingVendorVerifications, int PendingProductApprovals,
    int OpenSupportTickets, int PendingRefunds, int PendingPayoutRequests,
    decimal TotalPlatformCommissionEarned);

public record VendorAdminSummaryDto(
    Guid Id, string BusinessName, string ContactEmail, string VerificationStatus, string KycStatus,
    bool IsActive, decimal CommissionRatePercent, int ProductCount, DateTime CreatedAt);

public record CustomerAdminSummaryDto(
    Guid Id, string FullName, string Email, bool IsEmailVerified, int OrderCount, DateTime CreatedAt);

public record AuditLogEntryDto(
    Guid Id, Guid? UserId, string? UserRole, string Action, string EntityName, Guid EntityId,
    string? IpAddress, DateTime Timestamp);
