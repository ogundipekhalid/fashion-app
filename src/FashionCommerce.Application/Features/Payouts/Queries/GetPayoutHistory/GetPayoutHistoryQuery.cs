using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Features.Payouts.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Payouts.Queries.GetPayoutHistory;

public record GetPayoutHistoryQuery(Guid VendorId, int PageNumber = 1, int PageSize = 20) : IRequest<PaginatedList<PayoutRequestDto>>;

public class GetPayoutHistoryQueryHandler : IRequestHandler<GetPayoutHistoryQuery, PaginatedList<PayoutRequestDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPayoutHistoryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<PayoutRequestDto>> Handle(GetPayoutHistoryQuery request, CancellationToken cancellationToken)
    {
        var payouts = await _context.PayoutRequests
            .Where(p => p.VendorId == request.VendorId)
            .OrderByDescending(p => p.RequestedAt)
            .ToListAsync(cancellationToken);

        var dtos = payouts.Select(p => new PayoutRequestDto(
            p.Id, p.Amount, p.Currency, p.Status.ToString(),
            p.BankNameSnapshot, MaskAccountNumber(p.BankAccountNumberSnapshot),
            p.RequestedAt, p.ProcessedAt, p.FailureReason)).ToList();

        var pageItems = dtos.Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToList();
        return new PaginatedList<PayoutRequestDto>(pageItems, dtos.Count, request.PageNumber, request.PageSize);
    }

    private static string MaskAccountNumber(string accountNumber) =>
        accountNumber.Length <= 4 ? accountNumber : new string('*', accountNumber.Length - 4) + accountNumber[^4..];
}
