using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Domain.Entities.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Admin.Queries.GetPlatformSettings;

public record PlatformSettingsDto(
    decimal DefaultVendorCommissionRatePercent, decimal DefaultTaxRatePercent, decimal DefaultShippingFee,
    int PayoutHoldDays, string SupportEmail, string PlatformName, bool MaintenanceMode);

public record GetPlatformSettingsQuery : IRequest<PlatformSettingsDto>;

public class GetPlatformSettingsQueryHandler : IRequestHandler<GetPlatformSettingsQuery, PlatformSettingsDto>
{
    private readonly IApplicationDbContext _context;

    public GetPlatformSettingsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PlatformSettingsDto> Handle(GetPlatformSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await GetOrCreateAsync(_context, cancellationToken);

        return new PlatformSettingsDto(
            settings.DefaultVendorCommissionRatePercent, settings.DefaultTaxRatePercent, settings.DefaultShippingFee,
            settings.PayoutHoldDays, settings.SupportEmail, settings.PlatformName, settings.MaintenanceMode);
    }

    /// <summary>Shared by the query and the update command — covers a database that predates the migration-seeded row.</summary>
    internal static async Task<PlatformSettings> GetOrCreateAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var settings = await context.Settings.FirstOrDefaultAsync(s => s.Id == PlatformSettings.SingletonId, cancellationToken);
        if (settings is not null) return settings;

        settings = PlatformSettings.CreateDefault();
        context.Settings.Add(settings);
        await context.SaveChangesAsync(cancellationToken);
        return settings;
    }
}
