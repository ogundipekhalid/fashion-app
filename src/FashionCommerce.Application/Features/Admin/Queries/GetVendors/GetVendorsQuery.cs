using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Features.Admin.DTOs;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Admin.Queries.GetVendors;

public record GetVendorsQuery : IRequest<PaginatedList<VendorAdminSummaryDto>>
{
    public string? VerificationStatusFilter { get; init; }
    public string? SearchTerm { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetVendorsQueryHandler : IRequestHandler<GetVendorsQuery, PaginatedList<VendorAdminSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetVendorsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<VendorAdminSummaryDto>> Handle(GetVendorsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Vendors.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.VerificationStatusFilter) && Enum.TryParse<VendorVerificationStatus>(request.VerificationStatusFilter, out var status))
            query = query.Where(v => v.VerificationStatus == status);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            query = query.Where(v => v.BusinessName.Contains(request.SearchTerm) || v.ContactEmail.Contains(request.SearchTerm));

        var projected = query.OrderByDescending(v => v.CreatedAt).Select(v => new VendorAdminSummaryDto(
            v.Id, v.BusinessName, v.ContactEmail, v.VerificationStatus.ToString(), v.KycStatus.ToString(),
            v.IsActive, v.CommissionRatePercent, v.Products.Count, v.CreatedAt));

        return await PaginatedList<VendorAdminSummaryDto>.CreateAsync(projected, request.PageNumber, request.PageSize);
    }
}
