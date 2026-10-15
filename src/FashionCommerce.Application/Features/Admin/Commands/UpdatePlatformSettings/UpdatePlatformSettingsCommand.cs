using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Features.Admin.Queries.GetPlatformSettings;
using FluentValidation;
using MediatR;

namespace FashionCommerce.Application.Features.Admin.Commands.UpdatePlatformSettings;

public record UpdatePlatformSettingsCommand : IRequest<Result>, IAuditableRequest
{
    public decimal DefaultVendorCommissionRatePercent { get; init; }
    public decimal DefaultTaxRatePercent { get; init; }
    public decimal DefaultShippingFee { get; init; }
    public int PayoutHoldDays { get; init; }
    public string SupportEmail { get; init; } = default!;
    public string PlatformName { get; init; } = default!;
    public bool MaintenanceMode { get; init; }

    public string Action => "PlatformSettings.Updated";
    public string EntityName => "PlatformSettings";
}

public class UpdatePlatformSettingsCommandValidator : AbstractValidator<UpdatePlatformSettingsCommand>
{
    public UpdatePlatformSettingsCommandValidator()
    {
        RuleFor(x => x.DefaultVendorCommissionRatePercent).InclusiveBetween(0, 100);
        RuleFor(x => x.DefaultTaxRatePercent).InclusiveBetween(0, 100);
        RuleFor(x => x.DefaultShippingFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PayoutHoldDays).GreaterThanOrEqualTo(0).LessThanOrEqualTo(90);
        RuleFor(x => x.SupportEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.PlatformName).NotEmpty().MaximumLength(100);
    }
}

public class UpdatePlatformSettingsCommandHandler : IRequestHandler<UpdatePlatformSettingsCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public UpdatePlatformSettingsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(UpdatePlatformSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await GetPlatformSettingsQueryHandler.GetOrCreateAsync(_context, cancellationToken);

        settings.DefaultVendorCommissionRatePercent = request.DefaultVendorCommissionRatePercent;
        settings.DefaultTaxRatePercent = request.DefaultTaxRatePercent;
        settings.DefaultShippingFee = request.DefaultShippingFee;
        settings.PayoutHoldDays = request.PayoutHoldDays;
        settings.SupportEmail = request.SupportEmail;
        settings.PlatformName = request.PlatformName;
        settings.MaintenanceMode = request.MaintenanceMode;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
