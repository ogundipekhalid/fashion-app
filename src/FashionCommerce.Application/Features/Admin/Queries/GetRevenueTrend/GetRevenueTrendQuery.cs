using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Admin.Queries.GetRevenueTrend;

public enum TrendGranularity { Daily, Weekly, Monthly }

public record RevenueTrendPointDto(string PeriodLabel, DateTime PeriodStart, decimal Revenue, int OrderCount);

public record GetRevenueTrendQuery : IRequest<List<RevenueTrendPointDto>>
{
    public DateTime FromDate { get; init; } = DateTime.UtcNow.AddDays(-30);
    public DateTime ToDate { get; init; } = DateTime.UtcNow;
    public TrendGranularity Granularity { get; init; } = TrendGranularity.Daily;
}

public class GetRevenueTrendQueryHandler : IRequestHandler<GetRevenueTrendQuery, List<RevenueTrendPointDto>>
{
    private readonly IApplicationDbContext _context;

    public GetRevenueTrendQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<RevenueTrendPointDto>> Handle(GetRevenueTrendQuery request, CancellationToken cancellationToken)
    {
        // Deliberately pulled into memory before grouping: date-truncation
        // functions (DATEPART/DATE_TRUNC) don't translate identically across
        // EF Core providers, and the raw row count for a revenue-trend
        // window (days to a few months of orders) is small enough that
        // in-memory grouping is simpler and more portable than provider-specific SQL.
        var orders = await _context.Orders
            .Where(o => o.PlacedAt >= request.FromDate && o.PlacedAt <= request.ToDate)
            .Where(o => o.PaymentStatus == PaymentStatus.Successful || o.PaymentStatus == PaymentStatus.PartiallyRefunded)
            .Select(o => new { o.PlacedAt, o.Total })
            .ToListAsync(cancellationToken);

        return TrendBucketer.Bucket(orders.Select(o => (o.PlacedAt, o.Total)), request.Granularity);
    }
}

/// <summary>Shared bucketing logic between the Admin revenue trend and the Vendor sales trend — same grouping rules, different data source.</summary>
internal static class TrendBucketer
{
    public static List<RevenueTrendPointDto> Bucket(IEnumerable<(DateTime PlacedAt, decimal Total)> rows, TrendGranularity granularity)
    {
        return rows
            .GroupBy(r => BucketStart(r.PlacedAt, granularity))
            .OrderBy(g => g.Key)
            .Select(g => new RevenueTrendPointDto(FormatLabel(g.Key, granularity), g.Key, g.Sum(r => r.Total), g.Count()))
            .ToList();
    }

    private static DateTime BucketStart(DateTime date, TrendGranularity granularity) => granularity switch
    {
        TrendGranularity.Daily => date.Date,
        TrendGranularity.Weekly => date.Date.AddDays(-(int)date.DayOfWeek), // start of week (Sunday)
        TrendGranularity.Monthly => new DateTime(date.Year, date.Month, 1),
        _ => date.Date
    };

    private static string FormatLabel(DateTime bucketStart, TrendGranularity granularity) => granularity switch
    {
        TrendGranularity.Daily => bucketStart.ToString("yyyy-MM-dd"),
        TrendGranularity.Weekly => $"Week of {bucketStart:yyyy-MM-dd}",
        TrendGranularity.Monthly => bucketStart.ToString("yyyy-MM"),
        _ => bucketStart.ToString("yyyy-MM-dd")
    };
}
