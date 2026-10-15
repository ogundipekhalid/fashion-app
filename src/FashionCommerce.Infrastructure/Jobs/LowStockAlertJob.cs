using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Domain.Entities.Notifications;
using FashionCommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FashionCommerce.Infrastructure.Jobs;

/// <summary>
/// Runs periodically (see HangfireJobScheduler) and notifies each vendor
/// once per low-stock variant, per the spec's "Inventory Alerts"
/// requirement. Avoids re-notifying for the same variant every run by
/// checking for an existing unread ProductLowStock notification referencing
/// that variant in its metadata first.
/// </summary>
public class LowStockAlertJob
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<LowStockAlertJob> _logger;

    public LowStockAlertJob(IApplicationDbContext context, ILogger<LowStockAlertJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var lowStockVariants = await _context.ProductVariants
            .Include(v => v.Product)
            .Where(v => v.IsActive && v.StockQuantity <= v.LowStockThreshold)
            .ToListAsync(cancellationToken);

        var alertedCount = 0;

        foreach (var variant in lowStockVariants)
        {
            var metadataMarker = $"\"productVariantId\":\"{variant.Id}\"";
            var metadataJson = $"{{\"productId\":\"{variant.ProductId}\",{metadataMarker}}}";

            var alreadyNotified = await _context.Notifications.AnyAsync(n =>
                n.Type == NotificationType.ProductLowStock &&
                n.UserId == variant.Product.VendorId &&
                !n.IsRead &&
                n.MetadataJson != null && n.MetadataJson.Contains(metadataMarker),
                cancellationToken);

            if (alreadyNotified) continue;

            _context.Notifications.Add(new Notification
            {
                UserId = variant.Product.VendorId,
                UserRole = UserRole.Vendor,
                Type = NotificationType.ProductLowStock,
                Title = "Low stock alert",
                Body = $"'{variant.Product.Name}' ({variant.Color}/{variant.Size}) has only {variant.StockQuantity} unit(s) left.",
                MetadataJson = metadataJson,
                SentAt = DateTime.UtcNow
            });

            alertedCount++;
        }

        if (alertedCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("LowStockAlertJob: sent {Count} new low-stock alerts.", alertedCount);
        }
    }
}
