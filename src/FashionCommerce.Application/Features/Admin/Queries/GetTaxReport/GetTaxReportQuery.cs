using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using FashionCommerce.Application.Features.Admin.Queries.GetRevenueTrend;

namespace FashionCommerce.Application.Features.Admin.Queries.GetTaxReport;

public record TaxReportDto(
    DateTime FromDate, DateTime ToDate, int OrderCount,
    decimal TotalSubtotal, decimal TotalTaxCollected, decimal EffectiveTaxRatePercent);

public record GetTaxReportQuery : IRequest<TaxReportDto>
{
    public DateTime FromDate { get; init; } = DateTime.UtcNow.AddMonths(-1);
    public DateTime ToDate { get; init; } = DateTime.UtcNow;
}

public class GetTaxReportQueryHandler : IRequestHandler<GetTaxReportQuery, TaxReportDto>
{
    private readonly IApplicationDbContext _context;

    public GetTaxReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TaxReportDto> Handle(GetTaxReportQuery request, CancellationToken cancellationToken)
    {
        var fromDate = TrendBucketer.NormalizeUtc(request.FromDate);
        var toDate = TrendBucketer.NormalizeUtc(request.ToDate);
        var orders = await _context.Orders
            .Where(o => o.PlacedAt >= fromDate && o.PlacedAt <= toDate)
            .Where(o => o.PaymentStatus == PaymentStatus.Successful || o.PaymentStatus == PaymentStatus.PartiallyRefunded)
            .Select(o => new { o.Subtotal, o.TaxAmount })
            .ToListAsync(cancellationToken);

        var totalSubtotal = orders.Sum(o => o.Subtotal);
        var totalTax = orders.Sum(o => o.TaxAmount);
        var effectiveRate = totalSubtotal > 0 ? Math.Round(totalTax / totalSubtotal * 100m, 2) : 0m;

        return new TaxReportDto(fromDate, toDate, orders.Count, totalSubtotal, totalTax, effectiveRate);
    }
}
