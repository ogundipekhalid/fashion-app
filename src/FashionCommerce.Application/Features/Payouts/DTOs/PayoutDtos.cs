namespace FashionCommerce.Application.Features.Payouts.DTOs;

public record VendorWalletDto(
    decimal PendingBalance, decimal AvailableBalance, decimal WithdrawnBalance, decimal RefundBalance, string Currency);

public record PayoutRequestDto(
    Guid Id, decimal Amount, string Currency, string Status,
    string BankName, string BankAccountNumberMasked,
    DateTime RequestedAt, DateTime? ProcessedAt, string? FailureReason);
